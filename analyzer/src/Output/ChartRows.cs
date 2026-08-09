using BenchmarkAnalyzer.Models;

namespace BenchmarkAnalyzer.Output;

internal abstract class ChartRowBase
{
	protected ChartRowBase(ProtocolAggregate value)
	{
		Protocol = value.Protocol;
		Clients = value.Clients;
		PayloadSizeBytes = value.PayloadSizeBytes;
		MessageRatePerSecond = value.MessageRatePerSecond;
		DurationSeconds = value.DurationSeconds;
		TotalMessages = value.TotalMessages;
		RunCount = value.RunCount;
	}

	public string Protocol { get; }
	public int Clients { get; }
	public int PayloadSizeBytes { get; }
	public int MessageRatePerSecond { get; }
	public int DurationSeconds { get; }
	public long? TotalMessages { get; }
	public int RunCount { get; }
}

internal sealed class LatencyP95ChartRow : ChartRowBase
{
	public LatencyP95ChartRow(ProtocolAggregate value) : base(value)
	{
		AverageLatencyP95Ms = value.AverageLatencyP95Ms;
		LatencyP95StandardDeviationMs = value.LatencyP95StandardDeviationMs;
	}

	public double AverageLatencyP95Ms { get; }
	public double? LatencyP95StandardDeviationMs { get; }
}

internal sealed class LatencyP99ChartRow : ChartRowBase
{
	public LatencyP99ChartRow(ProtocolAggregate value) : base(value)
	{
		AverageLatencyP99Ms = value.AverageLatencyP99Ms;
		LatencyP99StandardDeviationMs = value.LatencyP99StandardDeviationMs;
	}

	public double AverageLatencyP99Ms { get; }
	public double? LatencyP99StandardDeviationMs { get; }
}

internal sealed class ThroughputChartRow : ChartRowBase
{
	public ThroughputChartRow(ProtocolAggregate value) : base(value)
	{
		AverageThroughputMessagesPerSecond = value.AverageThroughputMessagesPerSecond;
		ThroughputStandardDeviation = value.ThroughputStandardDeviation;
	}

	public double AverageThroughputMessagesPerSecond { get; }
	public double? ThroughputStandardDeviation { get; }
}

internal sealed class DeliveryRatioChartRow : ChartRowBase
{
	public DeliveryRatioChartRow(ProtocolAggregate value) : base(value)
	{
		AverageDeliveryRatio = value.AverageDeliveryRatio;
		DeliveryRatioStandardDeviation = value.DeliveryRatioStandardDeviation;
	}

	public double AverageDeliveryRatio { get; }
	public double? DeliveryRatioStandardDeviation { get; }
}

internal sealed class MessageLossChartRow : ChartRowBase
{
	public MessageLossChartRow(ProtocolAggregate value) : base(value)
	{
		AverageMessageLossRate = value.AverageMessageLossRate;
		MessageLossRateStandardDeviation = value.MessageLossRateStandardDeviation;
	}

	public double AverageMessageLossRate { get; }
	public double? MessageLossRateStandardDeviation { get; }
}

internal sealed class CpuChartRow : ChartRowBase
{
	public CpuChartRow(ProtocolAggregate value) : base(value)
	{
		AverageServerCpuPercent = value.AverageServerCpuPercent;
		ServerCpuStandardDeviationPercent = value.ServerCpuStandardDeviationPercent;
		PeakServerCpuPercent = value.PeakServerCpuPercent;
	}

	public double? AverageServerCpuPercent { get; }
	public double? ServerCpuStandardDeviationPercent { get; }
	public double? PeakServerCpuPercent { get; }
}

internal sealed class MemoryChartRow : ChartRowBase
{
	public MemoryChartRow(ProtocolAggregate value) : base(value)
	{
		AverageServerMemoryMB = value.AverageServerMemoryMB;
		ServerMemoryStandardDeviationMB = value.ServerMemoryStandardDeviationMB;
		PeakServerMemoryMB = value.PeakServerMemoryMB;
	}

	public double? AverageServerMemoryMB { get; }
	public double? ServerMemoryStandardDeviationMB { get; }
	public double? PeakServerMemoryMB { get; }
}

internal sealed class OverheadChartRow : ChartRowBase
{
	public OverheadChartRow(ProtocolAggregate value) : base(value)
	{
		AverageOverheadRatio = value.AverageOverheadRatio;
		OverheadRatioStandardDeviation = value.OverheadRatioStandardDeviation;
	}

	public double AverageOverheadRatio { get; }
	public double? OverheadRatioStandardDeviation { get; }
}
