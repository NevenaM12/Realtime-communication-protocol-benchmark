[CmdletBinding()]
param(
	[Parameter(Mandatory)]
	[ValidateNotNullOrEmpty()]
	[string]$ConfigPath,

	[string]$BatchName = "",
	[switch]$SkipBuild,
	[switch]$Resume
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "docker-experiment-common.ps1")

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if (-not [System.IO.Path]::IsPathRooted($ConfigPath)) {
	$ConfigPath = Join-Path $repositoryRoot $ConfigPath
}
$ConfigPath = (Resolve-Path $ConfigPath).Path
$configuration = Get-Content $ConfigPath -Raw | ConvertFrom-Json
$configurationHash = (Get-FileHash $ConfigPath -Algorithm SHA256).Hash.ToLowerInvariant()

if ([int]$configuration.repetitions -le 0) {
	throw "Matrix repetitions must be positive."
}
if ($configuration.scenarios.Count -eq 0) {
	throw "Matrix must contain at least one scenario."
}
if ([int]$configuration.warmupSeconds -lt 0 -or
	[int]$configuration.cooldownSeconds -lt 0 -or
	[int]$configuration.clockSyncSamples -le 0) {
	throw "Matrix warm-up and cooldown must be non-negative; clock-sync samples must be positive."
}

$protocols = @("ws", "sse", "lp")
if ($null -ne $configuration.PSObject.Properties["protocols"]) {
	$protocols = @($configuration.protocols | ForEach-Object { ([string]$_).ToLowerInvariant() })
	if ($protocols.Count -eq 0) {
		throw "Matrix protocols must contain at least one protocol."
	}
	$invalidProtocols = @($protocols | Where-Object { $_ -notin @("ws", "sse", "lp") })
	if ($invalidProtocols.Count -gt 0 -or @($protocols | Select-Object -Unique).Count -ne $protocols.Count) {
		throw "Matrix protocols must be unique values selected from ws, sse, and lp."
	}
}

$allowedDefaultProperties = @(
	"clientQueueCapacity", "messageBufferSize", "longPollMaxBatch")
$defaults = $configuration.PSObject.Properties["defaults"]
if ($null -ne $defaults) {
	$unknownDefaultProperties = @($configuration.defaults.PSObject.Properties.Name |
		Where-Object { $_ -notin $allowedDefaultProperties })
	if ($unknownDefaultProperties.Count -gt 0) {
		throw "Matrix defaults contain unknown properties: $($unknownDefaultProperties -join ', ')."
	}
}

$defaultClientQueueCapacity = if ($null -ne $defaults -and
	$null -ne $configuration.defaults.PSObject.Properties["clientQueueCapacity"]) {
	[int]$configuration.defaults.clientQueueCapacity
}
else { 4096 }
$defaultMessageBufferSize = if ($null -ne $defaults -and
	$null -ne $configuration.defaults.PSObject.Properties["messageBufferSize"]) {
	[int]$configuration.defaults.messageBufferSize
}
else { 4096 }
$defaultLongPollMaxBatch = if ($null -ne $defaults -and
	$null -ne $configuration.defaults.PSObject.Properties["longPollMaxBatch"]) {
	[int]$configuration.defaults.longPollMaxBatch
}
else { 100 }

if ($defaultClientQueueCapacity -le 0 -or $defaultMessageBufferSize -le 0 -or
	$defaultLongPollMaxBatch -le 0) {
	throw "Matrix default queue capacity, message buffer size, and long-poll maximum batch must be positive."
}

$scenarioNames = [System.Collections.Generic.HashSet[string]]::new(
	[System.StringComparer]::OrdinalIgnoreCase)
$allowedScenarioProperties = @(
	"name", "clients", "payloadSize", "rate", "durationSeconds", "totalMessages",
	"clientQueueCapacity", "messageBufferSize", "longPollMaxBatch")
foreach ($scenario in $configuration.scenarios) {
	$scenarioName = ConvertTo-SafeExperimentName ([string]$scenario.name)
	$unknownProperties = @($scenario.PSObject.Properties.Name | Where-Object { $_ -notin $allowedScenarioProperties })
	if ($unknownProperties.Count -gt 0) {
		throw "Scenario '$scenarioName' contains unknown properties: $($unknownProperties -join ', ')."
	}
	if (-not $scenarioNames.Add($scenarioName)) {
		throw "Matrix contains duplicate scenario name '$scenarioName'."
	}
	if ([int]$scenario.clients -le 0 -or [int]$scenario.payloadSize -lt 0 -or [int]$scenario.rate -le 0) {
		throw "Scenario '$scenarioName' must have positive clients and rate, and a non-negative payload size."
	}
	$scenarioDuration = if ($null -ne $scenario.PSObject.Properties["durationSeconds"]) {
		[int]$scenario.durationSeconds
	}
	else { 0 }
	$scenarioMessages = if ($null -ne $scenario.PSObject.Properties["totalMessages"]) {
		[long]$scenario.totalMessages
	}
	else { 0 }
	if ($scenarioDuration -lt 0 -or $scenarioMessages -lt 0 -or
		($scenarioDuration -eq 0 -and $scenarioMessages -eq 0)) {
		throw "Scenario '$scenarioName' must define a positive durationSeconds, totalMessages, or both."
	}
	$clientQueueCapacity = if ($null -ne $scenario.PSObject.Properties["clientQueueCapacity"]) {
		[int]$scenario.clientQueueCapacity
	}
	else { $defaultClientQueueCapacity }
	$messageBufferSize = if ($null -ne $scenario.PSObject.Properties["messageBufferSize"]) {
		[int]$scenario.messageBufferSize
	}
	else { $defaultMessageBufferSize }
	$longPollMaxBatch = if ($null -ne $scenario.PSObject.Properties["longPollMaxBatch"]) {
		[int]$scenario.longPollMaxBatch
	}
	else { $defaultLongPollMaxBatch }
	if ($clientQueueCapacity -le 0 -or $messageBufferSize -le 0 -or $longPollMaxBatch -le 0) {
		throw "Scenario '$scenarioName' queue capacity, message buffer size, and long-poll maximum batch must be positive."
	}
}

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
if ($Resume -and [string]::IsNullOrWhiteSpace($BatchName)) {
	throw "Resume requires the explicit -BatchName of an existing batch."
}
if ([string]::IsNullOrWhiteSpace($BatchName)) {
	$BatchName = "$($configuration.name)_$timestamp"
}
$BatchName = ConvertTo-SafeExperimentName $BatchName
$hostBatchDirectory = Join-Path $repositoryRoot "results\$BatchName"
$containerBatchDirectory = "/app/results/$BatchName"
$analysisDirectory = Join-Path $hostBatchDirectory "analysis"
$savedConfigurationPath = Join-Path $hostBatchDirectory "matrix-config.json"
$checkpointPath = Join-Path $hostBatchDirectory "matrix-checkpoint.json"
$manifestPath = Join-Path $hostBatchDirectory "matrix-execution.json"
$expectedRuns = [int]$configuration.repetitions * $configuration.scenarios.Count * $protocols.Count

if (Test-Path $hostBatchDirectory) {
	if (-not $Resume) {
		throw "Batch directory already exists: $hostBatchDirectory. Use -Resume to continue it."
	}
	if (-not (Test-Path $savedConfigurationPath -PathType Leaf)) {
		throw "Batch '$BatchName' cannot be resumed because matrix-config.json is missing."
	}

	$savedConfigurationHash = (Get-FileHash $savedConfigurationPath -Algorithm SHA256).Hash.ToLowerInvariant()
	if ($savedConfigurationHash -ne $configurationHash) {
		throw "Configuration mismatch for batch '$BatchName'. Resume it with the same matrix file that created it."
	}
	Write-Host "Resuming matrix batch '$BatchName'."
}
elseif ($Resume) {
	throw "Batch '$BatchName' does not exist and cannot be resumed."
}

Assert-DockerEngine
$buildProject = ConvertTo-SafeExperimentName "benchmark-matrix-$timestamp"
if (-not $SkipBuild) {
	Write-Host "Building Docker images once for the complete matrix..."
	Invoke-BenchmarkCompose $buildProject @("--profile", "tools", "build")
}

if (-not (Test-Path $hostBatchDirectory)) {
	New-Item -ItemType Directory -Path $hostBatchDirectory -Force | Out-Null
	Copy-Item $ConfigPath $savedConfigurationPath
}

$completedRuns = [System.Collections.Generic.List[object]]::new()
$scenarioIndex = 0

function Write-MatrixCheckpoint {
	param(
		[Parameter(Mandatory)]
		[string]$Status
	)

	$checkpoint = [ordered]@{
		batchName = $BatchName
		configurationHash = $configurationHash
		status = $Status
		updatedAtUtc = [DateTime]::UtcNow.ToString("O")
		expectedRunCount = $expectedRuns
		completedRunCount = $completedRuns.Count
		completedRuns = $completedRuns
	}
	$temporaryCheckpointPath = "$checkpointPath.tmp"
	$checkpoint | ConvertTo-Json -Depth 6 | Set-Content $temporaryCheckpointPath
	Move-Item -LiteralPath $temporaryCheckpointPath -Destination $checkpointPath -Force
}

function Move-IncompleteRunToArchive {
	param(
		[Parameter(Mandatory)]
		[string]$RunDirectory,
		[Parameter(Mandatory)]
		[string]$RunId
	)

	$batchRoot = [System.IO.Path]::GetFullPath($hostBatchDirectory).TrimEnd('\') + '\'
	$sourcePath = (Resolve-Path $RunDirectory).Path
	$archiveRoot = Join-Path $hostBatchDirectory "_incomplete"
	$archiveName = "$RunId.incomplete_$(Get-Date -Format 'yyyyMMdd_HHmmssfff')"
	$destinationPath = [System.IO.Path]::GetFullPath((Join-Path $archiveRoot $archiveName))

	if (-not $sourcePath.StartsWith($batchRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
		-not $destinationPath.StartsWith($batchRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
		throw "Refusing to move an incomplete run outside batch '$BatchName'."
	}

	New-Item -ItemType Directory -Path $archiveRoot -Force | Out-Null
	Move-Item -LiteralPath $sourcePath -Destination $destinationPath
	Write-Host "Preserved incomplete run at: $destinationPath"
}

Write-MatrixCheckpoint -Status "running"

foreach ($scenario in $configuration.scenarios) {
	$scenarioName = ConvertTo-SafeExperimentName ([string]$scenario.name)
	for ($repetition = 1; $repetition -le [int]$configuration.repetitions; $repetition++) {
		$rotation = ($scenarioIndex + $repetition - 1) % $protocols.Count
		$orderedProtocols = @(
			for ($index = 0; $index -lt $protocols.Count; $index++) {
				$protocols[($index + $rotation) % $protocols.Count]
			}
		)

		Write-Host "Scenario '$scenarioName', repetition $repetition protocol order: $($orderedProtocols -join ', ')"
		foreach ($protocol in $orderedProtocols) {
			$runId = "${scenarioName}_r$($repetition.ToString('00'))_$protocol"
			$hostRunDirectory = Join-Path $hostBatchDirectory $runId
			$durationSeconds = if ($null -ne $scenario.PSObject.Properties["durationSeconds"]) {
				[int]$scenario.durationSeconds
			}
			else {
				0
			}
			$totalMessages = if ($null -ne $scenario.PSObject.Properties["totalMessages"]) {
				[long]$scenario.totalMessages
			}
			else {
				0
			}
			$clientQueueCapacity = if ($null -ne $scenario.PSObject.Properties["clientQueueCapacity"]) {
				[int]$scenario.clientQueueCapacity
			}
			else { $defaultClientQueueCapacity }
			$messageBufferSize = if ($null -ne $scenario.PSObject.Properties["messageBufferSize"]) {
				[int]$scenario.messageBufferSize
			}
			else { $defaultMessageBufferSize }
			$longPollMaxBatch = if ($null -ne $scenario.PSObject.Properties["longPollMaxBatch"]) {
				[int]$scenario.longPollMaxBatch
			}
			else { $defaultLongPollMaxBatch }
			if ($durationSeconds -le 0 -and $totalMessages -le 0) {
				throw "Scenario '$scenarioName' must define a positive durationSeconds, totalMessages, or both."
			}
			$expectedMessages = if ($durationSeconds -eq 0) { $totalMessages } else { 0 }
			$runMetadata = [ordered]@{
				runId = $runId
				scenario = $scenarioName
				repetition = $repetition
				protocol = $protocol
				orderInRepetition = [array]::IndexOf($orderedProtocols, $protocol) + 1
				clientQueueCapacity = $clientQueueCapacity
				messageBufferSize = $messageBufferSize
				longPollMaxBatch = $longPollMaxBatch
			}

			if ($Resume -and (Test-Path (Join-Path $hostRunDirectory "final_summary.json") -PathType Leaf)) {
				try {
					$null = Assert-BenchmarkRunOutput `
						-RunDirectory $hostRunDirectory `
						-Protocol $protocol `
						-RunId $runId `
						-ExpectedMessages $expectedMessages
					$completedRuns.Add($runMetadata)
					Write-MatrixCheckpoint -Status "running"
					Write-Host "Skipping completed run '$runId'."
					continue
				}
				catch {
					Write-Warning "Run '$runId' has an invalid output and will be retried: $($_.Exception.Message)"
				}
			}

			if (Test-Path $hostRunDirectory) {
				if (-not $Resume) {
					throw "Run directory already exists: $hostRunDirectory"
				}
				Move-IncompleteRunToArchive -RunDirectory $hostRunDirectory -RunId $runId
			}

			$singleArguments = @{
				Protocol = $protocol
				Clients = [int]$scenario.clients
				PayloadSize = [int]$scenario.payloadSize
				Rate = [int]$scenario.rate
				DurationSeconds = $durationSeconds
				TotalMessages = $totalMessages
				WarmupSeconds = [int]$configuration.warmupSeconds
				CooldownSeconds = [int]$configuration.cooldownSeconds
				ClockSyncSamples = [int]$configuration.clockSyncSamples
				ClientQueueCapacity = $clientQueueCapacity
				MessageBufferSize = $messageBufferSize
				LongPollMaxBatch = $longPollMaxBatch
				BatchName = $BatchName
				RunId = $runId
				SkipBuild = $true
				SkipAnalysis = $true
			}

			& (Join-Path $PSScriptRoot "run-docker-single.ps1") @singleArguments
			$completedRuns.Add($runMetadata)
			Write-MatrixCheckpoint -Status "running"
		}
	}
	$scenarioIndex++
}

$manifest = [ordered]@{
	batchName = $BatchName
	configurationFile = [System.IO.Path]::GetFileName($ConfigPath)
	configurationHash = $configurationHash
	completedAtUtc = [DateTime]::UtcNow.ToString("O")
	runs = $completedRuns
}
$manifest | ConvertTo-Json -Depth 5 | Set-Content $manifestPath
Write-MatrixCheckpoint -Status "runs-complete"

$analysisProject = ConvertTo-SafeExperimentName "benchmark-analysis-$timestamp"
try {
	Invoke-BenchmarkCompose $analysisProject @(
		"--profile", "tools",
		"run", "--rm", "--no-deps", "analyzer",
		"--results-dir", $containerBatchDirectory,
		"--output-dir", "$containerBatchDirectory/analysis"
	)
}
finally {
	Invoke-BenchmarkCompose $analysisProject @("down", "--remove-orphans")
}

$analysis = Assert-AnalysisOutput `
	-AnalysisDirectory $analysisDirectory `
	-ExpectedRunCount $expectedRuns

$presentProtocols = @($analysis.ProtocolAggregates | Select-Object -ExpandProperty Protocol -Unique)
foreach ($protocol in $protocols) {
	if ($protocol -notin $presentProtocols) {
		throw "Analyzer output does not contain protocol '$protocol'."
	}
}

Write-MatrixCheckpoint -Status "completed"

Write-Host ""
Write-Host "Matrix completed successfully."
Write-Host "Runs: $($analysis.RunCount)"
Write-Host "Aggregates: $($analysis.ProtocolAggregates.Count)"
Write-Host "Results: $hostBatchDirectory"
Write-Host "Analysis: $analysisDirectory"
