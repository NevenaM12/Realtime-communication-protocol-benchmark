namespace LoadGenerator.Metrics;

public sealed class BenchmarkSummary
{
	public string RunId { get; set; } = "";
	public string Protocol { get; set; } = "";
	public int Clients { get; set; }
	public int PayloadSizeBytes { get; set; }
	public int MessageRatePerSecond { get; set; }
	public int DurationSeconds { get; set; }
	public long MessagesReceived { get; set; }
	public long UniqueMessagesReceived { get; set; }
	public long MessagesGeneratedByServer { get; set; }
	public long TheoreticalDeliveries { get; set; }
	public double DeliveryRatio { get; set; }
	public double ThroughputMessagesPerSecond { get; set; }
	public double LatencyAvgMs { get; set; }
	public double LatencyMedianMs { get; set; }
	public double LatencyP95Ms { get; set; }
	public double LatencyP99Ms { get; set; }
	public double LatencyMinMs { get; set; }
	public double LatencyMaxMs { get; set; }
	public double ConnectionSetupAvgMs { get; set; }
	public double ConnectionSetupP95Ms { get; set; }
	public long MessageLossCount { get; set; }
	public double MessageLossRate { get; set; }
	public long DuplicateMessageCount { get; set; }
	public long OutOfOrderMessageCount { get; set; }
	public long DisconnectCount { get; set; }
	public long ErrorCount { get; set; }
	public double EstimatedClockOffsetMs { get; set; }
	public double ClockSyncRttAvgMs { get; set; }
	public long PayloadBytesDelivered { get; set; }
	public long EncodedMessageBytes { get; set; }
	public long EstimatedProtocolBytes { get; set; }
	public long EstimatedOverheadBytes { get; set; }
	public double OverheadRatio { get; set; }
	public long PollRequests { get; set; }
	public long EmptyPollResponses { get; set; }
}
