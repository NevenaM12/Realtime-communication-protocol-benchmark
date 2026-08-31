using Xunit;
using LoadGenerator.Metrics;

namespace LoadGenerator.Tests;

public class LatencyHistogramTests
{
	[Fact]
	public void Calculates_average_range_and_percentiles()
	{
		var histogram = new LatencyHistogram();
		foreach (var value in Enumerable.Range(1, 100))
			histogram.Record(value);

		Assert.Equal(100, histogram.Count);
		Assert.Equal(50.5, histogram.Average, 3);
		Assert.Equal(1, histogram.Min);
		Assert.Equal(100, histogram.Max);
		Assert.InRange(histogram.Percentile(.5), 49.9, 50.1);
		Assert.InRange(histogram.Percentile(.95), 94.9, 95.1);
		Assert.InRange(histogram.Percentile(.99), 98.9, 99.1);
	}

	[Fact]
	public void Empty_histogram_returns_zero_values()
	{
		var histogram = new LatencyHistogram();

		Assert.Equal(0, histogram.Count);
		Assert.Equal(0, histogram.Average);
		Assert.Equal(0, histogram.Min);
		Assert.Equal(0, histogram.Max);
		Assert.Equal(0, histogram.Percentile(.95));
	}

	[Fact]
	public void Preserves_percentiles_above_sixty_seconds()
	{
		var histogram = new LatencyHistogram();
		histogram.Record(80_000);
		histogram.Record(100_000);
		histogram.Record(120_000);

		Assert.Equal(120_000, histogram.Percentile(.95));
		Assert.Equal(120_000, histogram.Max);
	}
}
