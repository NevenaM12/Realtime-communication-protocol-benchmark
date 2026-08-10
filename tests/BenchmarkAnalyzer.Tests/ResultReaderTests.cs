using BenchmarkAnalyzer.Services;
using Xunit;

namespace BenchmarkAnalyzer.Tests;

public class ResultReaderTests
{
	[Fact]
	public async Task Reads_complete_runs_and_derives_server_resource_metrics()
	{
		var root = CreateTemporaryDirectory();
		try
		{
			var completeRun = Directory.CreateDirectory(Path.Combine(root, "complete"));
			await File.WriteAllTextAsync(Path.Combine(completeRun.FullName, "final_summary.json"), """
			{
			  "runId": "ws-1",
			  "protocol": "ws",
			  "clients": 10,
			  "payloadSizeBytes": 1024,
			  "messageRatePerSecond": 100,
			  "durationSeconds": 60
			}
			""");
			await File.WriteAllTextAsync(Path.Combine(completeRun.FullName, "config.json"), """
			{ "totalMessages": 6000 }
			""");
			await File.WriteAllTextAsync(Path.Combine(completeRun.FullName, "server_resources.jsonl"), """
			{"process_cpu_percent":10,"process_memory_rss_bytes":104857600}
			{"process_cpu_percent":30,"process_memory_rss_bytes":209715200}
			""");

			var incompleteRun = Directory.CreateDirectory(Path.Combine(root, "incomplete"));
			await File.WriteAllTextAsync(Path.Combine(incompleteRun.FullName, "config.json"), "{}");

			var runs = await ResultReader.ReadAsync(root);

			var run = Assert.Single(runs);
			Assert.Equal("ws-1", run.RunId);
			Assert.Equal(6000, run.TotalMessages);
			Assert.True(run.HasServerResourceSamples);
			Assert.Equal(20, run.ServerCpuAvgPercent);
			Assert.Equal(30, run.ServerCpuPeakPercent);
			Assert.Equal(150, run.ServerMemoryAvgMB);
			Assert.Equal(200, run.ServerMemoryPeakMB);
		}
		finally
		{
			Directory.Delete(root, true);
		}
	}

	private static string CreateTemporaryDirectory()
	{
		var path = Path.Combine(Path.GetTempPath(), "BenchmarkAnalyzerTests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(path);
		return path;
	}
}
