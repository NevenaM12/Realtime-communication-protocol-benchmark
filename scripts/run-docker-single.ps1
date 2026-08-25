[CmdletBinding()]
param(
	[Parameter(Mandatory)]
	[ValidateSet("ws", "sse", "lp")]
	[string]$Protocol,
	[int]$Clients = 1,
	[int]$PayloadSize = 1024,
	[int]$Rate = 10,
	[int]$DurationSeconds = 0,
	[long]$TotalMessages = 100,
	[int]$WarmupSeconds = 5,
	[int]$CooldownSeconds = 2,
	[int]$ClockSyncSamples = 10,
	[string]$BatchName = "",
	[string]$RunId = "",
	[switch]$SkipBuild,
	[switch]$SkipAnalysis
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "docker-experiment-common.ps1")

if ($Clients -le 0 -or $PayloadSize -lt 0 -or $Rate -le 0) {
	throw "Clients and rate must be positive; payload size must be non-negative."
}
if ($DurationSeconds -lt 0 -or $TotalMessages -lt 0 -or ($DurationSeconds -eq 0 -and $TotalMessages -eq 0)) {
	throw "Specify a positive duration, a positive total message count, or both."
}
if ($WarmupSeconds -lt 0 -or $CooldownSeconds -lt 0 -or $ClockSyncSamples -le 0) {
	throw "Warm-up and cooldown must be non-negative; clock-sync samples must be positive."
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
if ([string]::IsNullOrWhiteSpace($BatchName)) {
	$BatchName = "single_$timestamp"
}
$BatchName = ConvertTo-SafeExperimentName $BatchName

if ([string]::IsNullOrWhiteSpace($RunId)) {
	$RunId = "${Protocol}_$timestamp"
}
$RunId = ConvertTo-SafeExperimentName $RunId

$hostBatchDirectory = Join-Path $repositoryRoot "results\$BatchName"
$hostRunDirectory = Join-Path $hostBatchDirectory $RunId
$containerBatchDirectory = "/app/results/$BatchName"
$analysisDirectory = Join-Path $hostBatchDirectory "analysis"
$composeProject = ConvertTo-SafeExperimentName "benchmark-$RunId"

if (Test-Path $hostRunDirectory) {
	throw "Run directory already exists: $hostRunDirectory"
}

Assert-DockerEngine
New-Item -ItemType Directory -Path $hostBatchDirectory -Force | Out-Null

$previousServerPort = $env:SERVER_PORT
$previousContainerResultsDirectory = $env:CONTAINER_RESULTS_DIR
$env:SERVER_PORT = (Get-AvailableTcpPort).ToString()
$env:CONTAINER_RESULTS_DIR = $containerBatchDirectory

try {
	if (-not $SkipBuild) {
		Write-Host "Building Docker images..."
		Invoke-BenchmarkCompose $composeProject @("--profile", "tools", "build")
	}

	Write-Host "Starting a fresh benchmark server for run '$RunId'..."
	Invoke-BenchmarkCompose $composeProject @("up", "-d", "--no-build", "benchmark-server")

	$loadGeneratorArguments = @(
		"--profile", "tools",
		"run", "--rm", "--no-deps", "load-generator",
		"--protocol", $Protocol,
		"--clients", $Clients.ToString(),
		"--payload-size", $PayloadSize.ToString(),
		"--rate", $Rate.ToString(),
		"--run-id", $RunId,
		"--server-url", "http://benchmark-server:8080",
		"--warmup-seconds", $WarmupSeconds.ToString(),
		"--cooldown-seconds", $CooldownSeconds.ToString(),
		"--output-dir", $containerBatchDirectory,
		"--setup-timeout-seconds", "60",
		"--clock-sync-samples", $ClockSyncSamples.ToString()
	)
	if ($DurationSeconds -gt 0) {
		$loadGeneratorArguments += @("--duration", $DurationSeconds.ToString())
	}
	if ($TotalMessages -gt 0) {
		$loadGeneratorArguments += @("--total-messages", $TotalMessages.ToString())
	}

	Write-Host "Running $Protocol benchmark '$RunId'..."
	Invoke-BenchmarkCompose $composeProject $loadGeneratorArguments

	$expectedMessages = if ($DurationSeconds -eq 0) { $TotalMessages } else { 0 }
	$summary = Assert-BenchmarkRunOutput `
		-RunDirectory $hostRunDirectory `
		-Protocol $Protocol `
		-RunId $RunId `
		-ExpectedMessages $expectedMessages

	if (-not $SkipAnalysis) {
		Write-Host "Analyzing batch '$BatchName'..."
		Invoke-BenchmarkCompose $composeProject @(
			"--profile", "tools",
			"run", "--rm", "--no-deps", "analyzer",
			"--results-dir", $containerBatchDirectory,
			"--output-dir", "$containerBatchDirectory/analysis"
		)
		$analysis = Assert-AnalysisOutput -AnalysisDirectory $analysisDirectory
		Write-Host "Analyzer currently sees $($analysis.RunCount) run(s) in this batch."
	}

	Write-Host (
		"Completed {0}: generated={1}, unique deliveries={2}, delivery ratio={3:P2}, p95={4:F2}ms" -f
		$RunId,
		$summary.MessagesGeneratedByServer,
		$summary.UniqueMessagesReceived,
		$summary.DeliveryRatio,
		$summary.LatencyP95Ms)
}
finally {
	try {
		Invoke-BenchmarkCompose $composeProject @("down", "--remove-orphans")
	}
	finally {
		$env:SERVER_PORT = $previousServerPort
		$env:CONTAINER_RESULTS_DIR = $previousContainerResultsDirectory
	}
}
