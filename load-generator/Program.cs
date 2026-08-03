using LoadGenerator.Cli;
using LoadGenerator.Services;

try
{
	var options = CommandLineParser.Parse(args);
	await new BenchmarkRunner(options).RunAsync();
}
catch (Exception ex)
{
	Console.Error.WriteLine(ex);
	Environment.ExitCode = 1;
}
