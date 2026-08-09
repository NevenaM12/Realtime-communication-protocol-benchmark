namespace BenchmarkAnalyzer.Models;

public sealed class RunSummary
{
	// run configuration
	public string RunId { get; set; } = "";
	public string Protocol { get; set; } = "";
	public int Clients { get; set; }
	public int PayloadSizeBytes { get; set; }
	public int MessageRatePerSecond { get; set; }
	public int DurationSeconds { get; set; }
	public long? TotalMessages { get; set; }

	// client metrics
	public long MessagesReceived { get; set; }
	public long UniqueMessagesReceived { get; set; }
	public long MessagesGeneratedByServer { get; set; }
	public long TheoreticalDeliveries { get; set; }
	public double DeliveryRatio { get; set; }
	public double ThroughputMessagesPerSecond { get; set; }

	// latency metrics
	public double LatencyAvgMs { get; set; }
	public double LatencyMedianMs { get; set; }
	public double LatencyP95Ms { get; set; }
	public double LatencyP99Ms { get; set; }
	public double LatencyMinMs { get; set; }
	public double LatencyMaxMs { get; set; }

	// setup and reliability metrics
	public double ConnectionSetupAvgMs { get; set; }
	public double ConnectionSetupP95Ms { get; set; }
	public long MessageLossCount { get; set; }
	public double MessageLossRate { get; set; }
	public long DuplicateMessageCount { get; set; }
	public long OutOfOrderMessageCount { get; set; }
	public long DisconnectCount { get; set; }
	public long ErrorCount { get; set; }

	// clock synchronization metrics
	public double EstimatedClockOffsetMs { get; set; }
	public double ClockSyncRttAvgMs { get; set; }

	// byte and long polling-specific metrics
	public long PayloadBytesDelivered { get; set; }
	public long EncodedMessageBytes { get; set; }
	public long EstimatedProtocolBytes { get; set; }
	public long EstimatedOverheadBytes { get; set; }
	public double OverheadRatio { get; set; }
	public long PollRequests { get; set; }
	public long EmptyPollResponses { get; set; }

	// server resource usage metrics
	public bool HasServerResourceSamples { get; set; }
	public double ServerCpuAvgPercent { get; set; }
	public double ServerCpuPeakPercent { get; set; }
	public double ServerMemoryAvgMB { get; set; }
	public double ServerMemoryPeakMB { get; set; }
}
