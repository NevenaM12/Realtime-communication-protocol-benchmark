using BenchmarkAnalyzer.Services;

try
{
	var resultsDir = "/app/results";
	string? outputDir = null;
	for (var i = 0; i < args.Length; i++)
	{
		if (args[i] == "--results-dir" && i + 1 < args.Length)
			resultsDir = args[++i];
		else if (args[i] == "--output-dir" && i + 1 < args.Length)
			outputDir = args[++i];
		else
			throw new ArgumentException($"Unknown or incomplete argument: {args[i]}");
	}

	var runs = await ResultReader.ReadAsync(resultsDir);
	var aggregates = ResultAggregator.Aggregate(runs);
	outputDir ??= Path.Combine(resultsDir, "analysis");
	await AnalysisWriter.WriteAsync(outputDir, runs, aggregates);

	Console.WriteLine($"Read {runs.Count} complete runs from {resultsDir}");
	Console.WriteLine($"Wrote {aggregates.Count} protocol aggregates to {outputDir}");
}
catch (Exception ex)
{
	Console.Error.WriteLine(ex);
	Environment.ExitCode = 1;
}
