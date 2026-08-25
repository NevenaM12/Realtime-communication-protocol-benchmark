function Invoke-BenchmarkCompose {
	param(
		[Parameter(Mandatory)]
		[string]$ProjectName,
		[Parameter(Mandatory)]
		[string[]]$ComposeArguments
	)

	& docker compose --project-name $ProjectName @ComposeArguments
	if ($LASTEXITCODE -ne 0) {
		throw "docker compose failed with exit code $LASTEXITCODE`: $($ComposeArguments -join ' ')"
	}
}

function Assert-DockerEngine {
	& docker info --format "{{.ServerVersion}}" | Out-Null
	if ($LASTEXITCODE -ne 0) {
		throw "Docker engine is not available. Start Docker Desktop and try again."
	}
}

function Get-AvailableTcpPort {
	$listener = [System.Net.Sockets.TcpListener]::new(
		[System.Net.IPAddress]::Loopback,
		0)
	try {
		$listener.Start()
		return ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
	}
	finally {
		$listener.Stop()
	}
}

function ConvertTo-SafeExperimentName {
	param(
		[Parameter(Mandatory)]
		[string]$Value
	)

	$safe = $Value.ToLowerInvariant() -replace "[^a-z0-9_.-]", "-"
	$safe = $safe.Trim("-", ".", "_")
	if ([string]::IsNullOrWhiteSpace($safe)) {
		throw "Experiment name '$Value' does not contain any supported characters."
	}
	return $safe
}

function Assert-BenchmarkRunOutput {
	param(
		[Parameter(Mandatory)]
		[string]$RunDirectory,
		[Parameter(Mandatory)]
		[string]$Protocol,
		[Parameter(Mandatory)]
		[string]$RunId,
		[long]$ExpectedMessages = 0
	)

	$requiredFiles = @(
		"config.json",
		"clock_sync.json",
		"server_config.json",
		"server_resources.jsonl",
		"server_final_stats.json",
		"client_metrics.json",
		"final_summary.json",
		"final_summary.csv"
	)

	foreach ($file in $requiredFiles) {
		if (-not (Test-Path (Join-Path $RunDirectory $file) -PathType Leaf)) {
			throw "Run '$RunId' did not produce required file '$file'."
		}
	}

	$resourceFile = Join-Path $RunDirectory "server_resources.jsonl"
	if ((Get-Item $resourceFile).Length -eq 0) {
		throw "Run '$RunId' did not collect a server resource sample."
	}

	$summary = Get-Content (Join-Path $RunDirectory "final_summary.json") -Raw | ConvertFrom-Json
	if ($summary.RunId -ne $RunId -or $summary.Protocol -ne $Protocol) {
		throw "Run '$RunId' contains summary metadata for a different run."
	}
	if ($ExpectedMessages -gt 0 -and [long]$summary.MessagesGeneratedByServer -ne $ExpectedMessages) {
		throw "Run '$RunId' generated $($summary.MessagesGeneratedByServer) messages; expected $ExpectedMessages."
	}
	if ([long]$summary.UniqueMessagesReceived -le 0) {
		throw "Run '$RunId' did not record any client deliveries."
	}

	return $summary
}

function Assert-AnalysisOutput {
	param(
		[Parameter(Mandatory)]
		[string]$AnalysisDirectory,
		[int]$ExpectedRunCount = 0
	)

	$requiredFiles = @(
		"run_summaries.json",
		"run_summaries.csv",
		"analysis_summary.json",
		"protocol_aggregates.csv",
		"chart-data\latency_p95_vs_clients.csv",
		"chart-data\latency_p99_vs_clients.csv",
		"chart-data\throughput_vs_clients.csv",
		"chart-data\generation_achievement_vs_message_rate.csv",
		"chart-data\delivery_ratio_vs_clients.csv",
		"chart-data\message_loss_vs_clients.csv",
		"chart-data\cpu_vs_clients.csv",
		"chart-data\memory_vs_clients.csv",
		"chart-data\overhead_vs_payload_size.csv"
	)

	foreach ($file in $requiredFiles) {
		if (-not (Test-Path (Join-Path $AnalysisDirectory $file) -PathType Leaf)) {
			throw "Analyzer did not produce required file '$file'."
		}
	}

	$analysis = Get-Content (Join-Path $AnalysisDirectory "analysis_summary.json") -Raw | ConvertFrom-Json
	if ($ExpectedRunCount -gt 0 -and [int]$analysis.RunCount -ne $ExpectedRunCount) {
		throw "Analyzer read $($analysis.RunCount) runs; expected $ExpectedRunCount."
	}

	return $analysis
}
