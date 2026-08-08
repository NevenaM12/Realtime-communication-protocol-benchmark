using BenchmarkAnalyzer.Services;

try
{
	var resultsDir = "/app/results";
	for (var i = 0; i < args.Length; i++)
	{
		if (args[i] == "--results-dir" && i + 1 < args.Length)
			resultsDir = args[++i];
		else
			throw new ArgumentException($"Unknown or incomplete argument: {args[i]}");
	}

	var runs = await ResultReader.ReadAsync(resultsDir);
	Console.WriteLine($"Read {runs.Count} complete runs from {resultsDir}");
}
catch (Exception ex)
{
	Console.Error.WriteLine(ex);
	Environment.ExitCode = 1;
}
