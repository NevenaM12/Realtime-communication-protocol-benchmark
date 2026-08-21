using Xunit;
using LoadGenerator.Metrics;
using BenchmarkShared;

namespace LoadGenerator.Tests;

public class RateTrackerTests
{
	[Fact]
	public void Calculates_deliveries_per_second()
	{
		Assert.Equal(50, ThroughputTracker.Calculate(100, 2));
		Assert.Equal(0, ThroughputTracker.Calculate(100, 0));
	}

	[Fact]
	public void Duration_limit_excludes_control_loop_overshoot()
	{
		Assert.Equal(5, ThroughputTracker.MeasurementSeconds(6.1, 5));
		Assert.Equal(3.2, ThroughputTracker.MeasurementSeconds(3.2, 5));
		Assert.Equal(6.1, ThroughputTracker.MeasurementSeconds(6.1, 0));
	}

	[Theory]
	[InlineData(100, 10, null, 1000L)]
	[InlineData(100, 0, 750L, 750L)]
	[InlineData(100, 10, 750L, 750L)]
	[InlineData(100, 10, 2000L, 1000L)]
	public void Calculates_generation_target_from_the_first_active_limit(
		int rate,
		int duration,
		long? totalMessages,
		long expected)
	{
		Assert.Equal(expected, GenerationTarget.Messages(rate, duration, totalMessages));
	}

	[Fact]
	public void Calculates_generation_achievement_without_hiding_shortfall()
	{
		Assert.Equal(0.9, GenerationTarget.AchievementRatio(900, 1000), 5);
		Assert.Equal(0, GenerationTarget.AchievementRatio(900, 0));
	}

	[Fact]
	public void Calculates_average_and_p95_setup_time()
	{
		var setupTimes = Enumerable.Range(1, 100).Select(x => (double)x).ToArray();

		Assert.Equal(50.5, SetupTimeTracker.Average(setupTimes));
		Assert.Equal(95, SetupTimeTracker.P95(setupTimes));
	}

	[Fact]
	public void Empty_setup_times_return_zero()
	{
		Assert.Equal(0, SetupTimeTracker.Average([]));
		Assert.Equal(0, SetupTimeTracker.P95([]));
	}
}
