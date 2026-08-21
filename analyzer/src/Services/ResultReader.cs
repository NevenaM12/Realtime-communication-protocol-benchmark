using System.Text.Json;
using System.Text.Json.Serialization;
using BenchmarkAnalyzer.Models;
using BenchmarkShared;

namespace BenchmarkAnalyzer.Services;

public static class ResultReader
{
	private static readonly JsonSerializerOptions Json = new()
	{
		PropertyNameCaseInsensitive = true
	};

	public static async Task<IReadOnlyList<RunSummary>> ReadAsync(string root)
	{
		if (!Directory.Exists(root))
			return [];

		var runs = new List<RunSummary>();
		foreach (var directory in Directory.EnumerateDirectories(root))
		{
			var run = await TryReadRunAsync(directory);
			if (run is not null)
				runs.Add(run);
		}

		return runs
			.OrderBy(run => run.Protocol)
			.ThenBy(run => run.Clients)
			.ThenBy(run => run.PayloadSizeBytes)
			.ThenBy(run => run.MessageRatePerSecond)
			.ThenBy(run => run.RunId)
			.ToArray();
	}

	private static async Task<RunSummary?> TryReadRunAsync(string directory)
	{
		var finalSummaryPath = Path.Combine(directory, "final_summary.json");
		if (!File.Exists(finalSummaryPath))
			return null;

		try
		{
			var summary = JsonSerializer.Deserialize<RunSummary>(
				await File.ReadAllTextAsync(finalSummaryPath), Json);
			if (summary is null || string.IsNullOrWhiteSpace(summary.RunId))
				return null;

			summary.TotalMessages = await ReadTotalMessagesAsync(directory);
			summary.TargetMessages = GenerationTarget.Messages(
				summary.MessageRatePerSecond,
				summary.DurationSeconds,
				summary.TotalMessages);
			summary.GenerationAchievementRatio = summary.TargetMessages <= 0
				? 0
				: summary.MessagesGeneratedByServer / (double)summary.TargetMessages;

			var resourceSamples = await ReadResourceSamplesAsync(directory);
			if (resourceSamples.Count > 0)
			{
				summary.HasServerResourceSamples = true;
				summary.ServerCpuAvgPercent = resourceSamples.Average(sample => sample.ProcessCpuPercent);
				summary.ServerCpuPeakPercent = resourceSamples.Max(sample => sample.ProcessCpuPercent);
				summary.ServerMemoryAvgMB = resourceSamples.Average(sample => sample.ProcessMemoryRssBytes) / 1024d / 1024d;
				summary.ServerMemoryPeakMB = resourceSamples.Max(sample => sample.ProcessMemoryRssBytes) / 1024d / 1024d;
			}

			return summary;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
		{
			Console.Error.WriteLine($"Ignoring incomplete or corrupt run '{directory}': {ex.Message}");
			return null;
		}
	}

	private static async Task<long?> ReadTotalMessagesAsync(string directory)
	{
		var path = Path.Combine(directory, "config.json");
		if (!File.Exists(path))
			return null;

		try
		{
			var configuration = JsonSerializer.Deserialize<RunConfiguration>(
				await File.ReadAllTextAsync(path), Json);
			return configuration?.TotalMessages;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
		{
			Console.Error.WriteLine($"Could not read optional configuration for '{directory}': {ex.Message}");
			return null;
		}
	}

	private static async Task<List<ResourceSample>> ReadResourceSamplesAsync(string directory)
	{
		var path = Path.Combine(directory, "server_resources.jsonl");
		if (!File.Exists(path))
			return [];

		var samples = new List<ResourceSample>();
		await foreach (var line in File.ReadLinesAsync(path))
		{
			if (string.IsNullOrWhiteSpace(line))
				continue;

			var sample = JsonSerializer.Deserialize<ResourceSample>(line, Json);
			if (sample is not null)
				samples.Add(sample);
		}

		return samples;
	}

	private sealed class ResourceSample
	{
		[JsonPropertyName("process_cpu_percent")]
		public double ProcessCpuPercent { get; set; }

		[JsonPropertyName("process_memory_rss_bytes")]
		public long ProcessMemoryRssBytes { get; set; }
	}

	private sealed class RunConfiguration
	{
		public long? TotalMessages { get; set; }
	}
}
