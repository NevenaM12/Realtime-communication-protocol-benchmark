namespace BenchmarkAnalyzer.Models;

// One aggregate over repeated runs with the same protocol and workload configuration.
public sealed class ProtocolAggregate
{
	public string Protocol { get; set; } = "";
	public int Clients { get; set; }
	public int PayloadSizeBytes { get; set; }
	public int MessageRatePerSecond { get; set; }
	public int DurationSeconds { get; set; }
	public long? TotalMessages { get; set; }
	public int RunCount { get; set; }
	public int ClientQueueCapacity { get; set; }
	public int MessageBufferSize { get; set; }
	public int LongPollMaxBatch { get; set; }
	public double AverageThroughputMessagesPerSecond { get; set; }
	public double? ThroughputStandardDeviation { get; set; }
	public double AverageGenerationAchievementRatio { get; set; }
	public double? GenerationAchievementRatioStandardDeviation { get; set; }
	public double AverageDeliveryRatio { get; set; }
	public double? DeliveryRatioStandardDeviation { get; set; }
	public double AverageMessageLossRate { get; set; }
	public double? MessageLossRateStandardDeviation { get; set; }
	public double AverageLatencyAvgMs { get; set; }
	public double AverageLatencyMedianMs { get; set; }
	public double AverageLatencyP95Ms { get; set; }
	public double? LatencyP95StandardDeviationMs { get; set; }
	public double AverageLatencyP99Ms { get; set; }
	public double? LatencyP99StandardDeviationMs { get; set; }
	public double AverageConnectionSetupMs { get; set; }
	public double AverageOverheadRatio { get; set; }
	public double? OverheadRatioStandardDeviation { get; set; }
	public double? AverageServerCpuPercent { get; set; }
	public double? ServerCpuStandardDeviationPercent { get; set; }
	public double? PeakServerCpuPercent { get; set; }
	public double? AverageServerMemoryMB { get; set; }
	public double? ServerMemoryStandardDeviationMB { get; set; }
	public double? PeakServerMemoryMB { get; set; }
}
