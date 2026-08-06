using Xunit;
using LoadGenerator.Metrics;

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
