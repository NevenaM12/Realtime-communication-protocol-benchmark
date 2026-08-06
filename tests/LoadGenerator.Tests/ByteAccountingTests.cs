using Xunit;
using LoadGenerator.Metrics;

namespace LoadGenerator.Tests;

public class ByteAccountingTests
{
	[Fact]
	public void Accumulates_bytes_and_calculates_overhead()
	{
		var accounting = new ByteAccounting();

		accounting.Add(100, 120, 125);
		accounting.Add(50, 60, 65);

		Assert.Equal(150, accounting.PayloadBytes);
		Assert.Equal(180, accounting.EncodedMessageBytes);
		Assert.Equal(190, accounting.EstimatedProtocolBytes);
		Assert.Equal(40, accounting.EstimatedOverheadBytes);
		Assert.Equal(40 / 150d, accounting.OverheadRatio, 6);
	}

	[Fact]
	public void Overhead_is_never_negative()
	{
		var accounting = new ByteAccounting();
		accounting.Add(100, 90, 90);

		Assert.Equal(0, accounting.EstimatedOverheadBytes);
		Assert.Equal(0, accounting.OverheadRatio);
	}

	[Fact]
	public void Concurrent_updates_are_not_lost()
	{
		var accounting = new ByteAccounting();

		Parallel.For(0, 1000, _ => accounting.Add(1, 2, 3));

		Assert.Equal(1000, accounting.PayloadBytes);
		Assert.Equal(2000, accounting.EncodedMessageBytes);
		Assert.Equal(3000, accounting.EstimatedProtocolBytes);
	}
}
