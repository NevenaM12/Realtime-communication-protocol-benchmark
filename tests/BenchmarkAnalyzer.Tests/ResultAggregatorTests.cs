using BenchmarkAnalyzer.Models;
using BenchmarkAnalyzer.Services;
using Xunit;

namespace BenchmarkAnalyzer.Tests;

public class ResultAggregatorTests
{
	[Fact]
	public void Aggregates_repeated_runs_with_the_same_protocol_and_workload()
	{
		var runs = new[]
		{
			CreateRun("ws", throughput: 10, deliveryRatio: 0.8, generationAchievementRatio: 0.9, latencyP95: 10, cpuPeak: 20, memoryPeak: 100, cpuAverage: 10),
			CreateRun("ws", throughput: 20, deliveryRatio: 0.6, generationAchievementRatio: 0.7, latencyP95: 30, cpuPeak: 40, memoryPeak: 300, cpuAverage: 20),
			CreateRun("sse", throughput: 5, deliveryRatio: 0.9, generationAchievementRatio: 1, latencyP95: 15, cpuPeak: 25, memoryPeak: 150, cpuAverage: 15)
		};

		var aggregates = ResultAggregator.Aggregate(runs);

		Assert.Equal(2, aggregates.Count);
		var websocket = Assert.Single(aggregates, aggregate => aggregate.Protocol == "ws");
		Assert.Equal(2, websocket.RunCount);
		Assert.Equal(15, websocket.AverageThroughputMessagesPerSecond);
		Assert.Equal(Math.Sqrt(50), websocket.ThroughputStandardDeviation!.Value, 5);
		Assert.Equal(0.8, websocket.AverageGenerationAchievementRatio, 5);
		Assert.Equal(Math.Sqrt(0.02), websocket.GenerationAchievementRatioStandardDeviation!.Value, 5);
		Assert.Equal(0.7, websocket.AverageDeliveryRatio, 5);
		Assert.Equal(20, websocket.AverageLatencyP95Ms);
		Assert.Equal(Math.Sqrt(200), websocket.LatencyP95StandardDeviationMs!.Value, 5);
		Assert.Equal(40, websocket.PeakServerCpuPercent!.Value);
		Assert.Equal(300, websocket.PeakServerMemoryMB!.Value);
	}

	[Fact]
	public void Leaves_server_resource_aggregates_empty_when_samples_are_unavailable()
	{
		var run = CreateRun("lp", throughput: 10, deliveryRatio: 1, latencyP95: 2,
			generationAchievementRatio: 1, cpuPeak: 0, memoryPeak: 0, cpuAverage: 0);
		run.HasServerResourceSamples = false;

		var aggregate = Assert.Single(ResultAggregator.Aggregate([run]));

		Assert.Null(aggregate.AverageServerCpuPercent);
		Assert.Null(aggregate.PeakServerCpuPercent);
		Assert.Null(aggregate.AverageServerMemoryMB);
		Assert.Null(aggregate.PeakServerMemoryMB);
		Assert.Null(aggregate.ThroughputStandardDeviation);
		Assert.Null(aggregate.DeliveryRatioStandardDeviation);
		Assert.Null(aggregate.LatencyP95StandardDeviationMs);
	}

	[Fact]
	public void Keeps_runs_with_different_transport_limits_in_separate_aggregates()
	{
		var first = CreateRun("lp", throughput: 10, deliveryRatio: 1,
			generationAchievementRatio: 1, latencyP95: 2, cpuPeak: 20,
			memoryPeak: 100, cpuAverage: 10);
		first.ClientQueueCapacity = 2500;
		first.MessageBufferSize = 10000;
		first.LongPollMaxBatch = 500;

		var second = CreateRun("lp", throughput: 10, deliveryRatio: 1,
			generationAchievementRatio: 1, latencyP95: 2, cpuPeak: 20,
			memoryPeak: 100, cpuAverage: 10);
		second.ClientQueueCapacity = 2500;
		second.MessageBufferSize = 10000;
		second.LongPollMaxBatch = 1000;

		var aggregates = ResultAggregator.Aggregate([first, second]);

		Assert.Equal(2, aggregates.Count);
		Assert.Contains(aggregates, aggregate => aggregate.LongPollMaxBatch == 500);
		Assert.Contains(aggregates, aggregate => aggregate.LongPollMaxBatch == 1000);
	}

	private static RunSummary CreateRun(
		string protocol,
		double throughput,
		double deliveryRatio,
		double generationAchievementRatio,
		double latencyP95,
		double cpuPeak,
		double memoryPeak,
		double cpuAverage) => new()
	{
		Protocol = protocol,
		Clients = 10,
		PayloadSizeBytes = 1024,
		MessageRatePerSecond = 100,
		DurationSeconds = 60,
		TotalMessages = null,
		ThroughputMessagesPerSecond = throughput,
		GenerationAchievementRatio = generationAchievementRatio,
		DeliveryRatio = deliveryRatio,
		LatencyP95Ms = latencyP95,
		HasServerResourceSamples = true,
		ServerCpuPeakPercent = cpuPeak,
		ServerMemoryPeakMB = memoryPeak,
		ServerCpuAvgPercent = cpuAverage
	};
}
