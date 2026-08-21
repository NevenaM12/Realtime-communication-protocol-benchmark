using BenchmarkAnalyzer.Models;

namespace BenchmarkAnalyzer.Output;

internal static class ChartDataWriter
{
	public static async Task WriteAsync(string outputDir, IReadOnlyList<ProtocolAggregate> aggregates)
	{
		Directory.CreateDirectory(outputDir);

		await CsvWriter.WriteAsync(
			Path.Combine(outputDir, "latency_p95_vs_clients.csv"),
			aggregates.Select(value => new LatencyP95ChartRow(value)));
		await CsvWriter.WriteAsync(
			Path.Combine(outputDir, "latency_p99_vs_clients.csv"),
			aggregates.Select(value => new LatencyP99ChartRow(value)));
		await CsvWriter.WriteAsync(
			Path.Combine(outputDir, "throughput_vs_clients.csv"),
			aggregates.Select(value => new ThroughputChartRow(value)));
		await CsvWriter.WriteAsync(
			Path.Combine(outputDir, "generation_achievement_vs_message_rate.csv"),
			aggregates.Select(value => new GenerationAchievementChartRow(value)));
		await CsvWriter.WriteAsync(
			Path.Combine(outputDir, "delivery_ratio_vs_clients.csv"),
			aggregates.Select(value => new DeliveryRatioChartRow(value)));
		await CsvWriter.WriteAsync(
			Path.Combine(outputDir, "message_loss_vs_clients.csv"),
			aggregates.Select(value => new MessageLossChartRow(value)));
		await CsvWriter.WriteAsync(
			Path.Combine(outputDir, "cpu_vs_clients.csv"),
			aggregates.Select(value => new CpuChartRow(value)));
		await CsvWriter.WriteAsync(
			Path.Combine(outputDir, "memory_vs_clients.csv"),
			aggregates.Select(value => new MemoryChartRow(value)));
		await CsvWriter.WriteAsync(
			Path.Combine(outputDir, "overhead_vs_payload_size.csv"),
			aggregates.Select(value => new OverheadChartRow(value)));
	}
}
