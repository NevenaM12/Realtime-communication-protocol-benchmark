using BenchmarkAnalyzer.Models;

namespace BenchmarkAnalyzer.Services;

public static class ResultAggregator
{
	public static IReadOnlyList<ProtocolAggregate> Aggregate(IReadOnlyList<RunSummary> runs) =>
		runs
			.GroupBy(run => new AggregateKey(
				run.Protocol,
				run.Clients,
				run.PayloadSizeBytes,
				run.MessageRatePerSecond,
				run.DurationSeconds,
				run.TotalMessages))
			.Select(CreateAggregate)
			.OrderBy(aggregate => aggregate.Protocol)
			.ThenBy(aggregate => aggregate.Clients)
			.ThenBy(aggregate => aggregate.PayloadSizeBytes)
			.ThenBy(aggregate => aggregate.MessageRatePerSecond)
			.ThenBy(aggregate => aggregate.DurationSeconds)
			.ThenBy(aggregate => aggregate.TotalMessages)
			.ToArray();

	private static ProtocolAggregate CreateAggregate(IGrouping<AggregateKey, RunSummary> group)
	{
		var runs = group.ToArray();
		var resourceRuns = runs.Where(run => run.HasServerResourceSamples).ToArray();

		return new ProtocolAggregate
		{
			Protocol = group.Key.Protocol,
			Clients = group.Key.Clients,
			PayloadSizeBytes = group.Key.PayloadSizeBytes,
			MessageRatePerSecond = group.Key.MessageRatePerSecond,
			DurationSeconds = group.Key.DurationSeconds,
			TotalMessages = group.Key.TotalMessages,
			RunCount = runs.Length,
			AverageThroughputMessagesPerSecond = runs.Average(run => run.ThroughputMessagesPerSecond),
			ThroughputStandardDeviation = SampleStandardDeviation(runs.Select(run => run.ThroughputMessagesPerSecond)),
			AverageGenerationAchievementRatio = runs.Average(run => run.GenerationAchievementRatio),
			GenerationAchievementRatioStandardDeviation = SampleStandardDeviation(
				runs.Select(run => run.GenerationAchievementRatio)),
			AverageDeliveryRatio = runs.Average(run => run.DeliveryRatio),
			DeliveryRatioStandardDeviation = SampleStandardDeviation(runs.Select(run => run.DeliveryRatio)),
			AverageMessageLossRate = runs.Average(run => run.MessageLossRate),
			MessageLossRateStandardDeviation = SampleStandardDeviation(runs.Select(run => run.MessageLossRate)),
			AverageLatencyAvgMs = runs.Average(run => run.LatencyAvgMs),
			AverageLatencyMedianMs = runs.Average(run => run.LatencyMedianMs),
			AverageLatencyP95Ms = runs.Average(run => run.LatencyP95Ms),
			LatencyP95StandardDeviationMs = SampleStandardDeviation(runs.Select(run => run.LatencyP95Ms)),
			AverageLatencyP99Ms = runs.Average(run => run.LatencyP99Ms),
			LatencyP99StandardDeviationMs = SampleStandardDeviation(runs.Select(run => run.LatencyP99Ms)),
			AverageConnectionSetupMs = runs.Average(run => run.ConnectionSetupAvgMs),
			AverageOverheadRatio = runs.Average(run => run.OverheadRatio),
			OverheadRatioStandardDeviation = SampleStandardDeviation(runs.Select(run => run.OverheadRatio)),
			AverageServerCpuPercent = AverageOrNull(resourceRuns.Select(run => run.ServerCpuAvgPercent)),
			ServerCpuStandardDeviationPercent = StandardDeviationOrNull(resourceRuns.Select(run => run.ServerCpuAvgPercent)),
			PeakServerCpuPercent = MaxOrNull(resourceRuns.Select(run => run.ServerCpuPeakPercent)),
			AverageServerMemoryMB = AverageOrNull(resourceRuns.Select(run => run.ServerMemoryAvgMB)),
			ServerMemoryStandardDeviationMB = StandardDeviationOrNull(resourceRuns.Select(run => run.ServerMemoryAvgMB)),
			PeakServerMemoryMB = MaxOrNull(resourceRuns.Select(run => run.ServerMemoryPeakMB))
		};
	}

	private static double? SampleStandardDeviation(IEnumerable<double> source)
	{
		var values = source.ToArray();
		if (values.Length < 2)
			return null;

		var average = values.Average();
		var sumOfSquaredDifferences = values.Sum(value => Math.Pow(value - average, 2));
		return Math.Sqrt(sumOfSquaredDifferences / (values.Length - 1));
	}

	private static double? AverageOrNull(IEnumerable<double> source)
	{
		var values = source.ToArray();
		return values.Length == 0 ? null : values.Average();
	}

	private static double? MaxOrNull(IEnumerable<double> source)
	{
		var values = source.ToArray();
		return values.Length == 0 ? null : values.Max();
	}

	private static double? StandardDeviationOrNull(IEnumerable<double> source)
	{
		return SampleStandardDeviation(source);
	}

	private readonly record struct AggregateKey(
		string Protocol,
		int Clients,
		int PayloadSizeBytes,
		int MessageRatePerSecond,
		int DurationSeconds,
		long? TotalMessages);
}
