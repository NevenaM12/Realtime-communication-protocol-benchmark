using BenchmarkAnalyzer.Models;
using BenchmarkAnalyzer.Services;
using Xunit;

namespace BenchmarkAnalyzer.Tests;

public class AnalysisWriterTests
{
	[Fact]
	public async Task Writes_run_summary_aggregate_and_chart_data_files()
	{
		var output = Path.Combine(Path.GetTempPath(), "BenchmarkAnalyzerTests", Guid.NewGuid().ToString("N"));
		try
		{
			var runs = new[]
			{
				new RunSummary
				{
					RunId = "ws-1", Protocol = "ws", Clients = 1,
					PayloadSizeBytes = 128, MessageRatePerSecond = 10,
					DurationSeconds = 30, ThroughputMessagesPerSecond = 9.5
				}
			};
			var aggregates = ResultAggregator.Aggregate(runs);

			await AnalysisWriter.WriteAsync(output, runs, aggregates);

			Assert.True(File.Exists(Path.Combine(output, "run_summaries.json")));
			Assert.True(File.Exists(Path.Combine(output, "run_summaries.csv")));
			Assert.True(File.Exists(Path.Combine(output, "analysis_summary.json")));
			Assert.True(File.Exists(Path.Combine(output, "protocol_aggregates.csv")));
			var chartDataDir = Path.Combine(output, "chart-data");
			var metrics = new[]
			{
				"latency_p95", "latency_p99", "throughput", "generation_achievement",
				"delivery_ratio", "message_loss", "cpu", "memory", "overhead"
			};
			var axes = new[] { "clients", "message_rate", "payload_size" };
			var expectedChartFiles = metrics
				.SelectMany(metric => axes.Select(axis => $"{metric}_vs_{axis}.csv"))
				.ToArray();
			foreach (var file in expectedChartFiles)
				Assert.True(File.Exists(Path.Combine(chartDataDir, file)), $"Missing chart data file: {file}");
			Assert.Equal(27, Directory.EnumerateFiles(chartDataDir, "*.csv").Count());

			var throughputCsv = await File.ReadAllTextAsync(Path.Combine(chartDataDir, "throughput_vs_clients.csv"));
			Assert.StartsWith("Protocol,Clients,PayloadSizeBytes,MessageRatePerSecond,DurationSeconds,TotalMessages,RunCount", throughputCsv);
			Assert.Contains("AverageThroughputMessagesPerSecond", throughputCsv);
			Assert.Contains("ThroughputStandardDeviation", throughputCsv);
			Assert.Contains("ws", throughputCsv);
		}
		finally
		{
			if (Directory.Exists(output))
				Directory.Delete(output, true);
		}
	}
}
