using BenchmarkAnalyzer.Models;
using BenchmarkAnalyzer.Output;

namespace BenchmarkAnalyzer.Services;

// Coordinates analyzer output; format-specific details live in the Output folder.
public static class AnalysisWriter
{
	public static async Task WriteAsync(
		string outputDir,
		IReadOnlyList<RunSummary> runs,
		IReadOnlyList<ProtocolAggregate> aggregates)
	{
		Directory.CreateDirectory(outputDir);

		await JsonWriter.WriteAsync(Path.Combine(outputDir, "run_summaries.json"), runs);
		await CsvWriter.WriteAsync(Path.Combine(outputDir, "run_summaries.csv"), runs);

		var report = new AnalysisReport
		{
			GeneratedAtUtc = DateTime.UtcNow,
			RunCount = runs.Count,
			ProtocolAggregates = aggregates
		};
		await JsonWriter.WriteAsync(Path.Combine(outputDir, "analysis_summary.json"), report);
		await CsvWriter.WriteAsync(Path.Combine(outputDir, "protocol_aggregates.csv"), aggregates);
		await ChartDataWriter.WriteAsync(Path.Combine(outputDir, "chart-data"), aggregates);
	}
}
