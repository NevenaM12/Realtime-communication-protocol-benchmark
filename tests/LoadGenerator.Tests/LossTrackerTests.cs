using Xunit;
using LoadGenerator.Metrics;

namespace LoadGenerator.Tests;

public class LossTrackerTests
{
	[Fact]
	public void Late_arrivals_reduce_missing_count()
	{
		var tracker = new LossTracker();

		foreach (var id in new long[] { 1, 2, 5 })
			tracker.Record(id);

		Assert.Equal(2, tracker.Missing);

		tracker.Record(4);
		Assert.Equal(1, tracker.Missing);
		Assert.Equal(1, tracker.OutOfOrder);

		tracker.Record(3);
		Assert.Equal(0, tracker.Missing);
		Assert.Equal(2, tracker.OutOfOrder);
	}

	[Fact]
	public void Counts_duplicates_without_increasing_unique_count()
	{
		var tracker = new LossTracker();

		foreach (var id in new long[] { 1, 2, 2 })
			tracker.Record(id);

		Assert.Equal(3, tracker.Count);
		Assert.Equal(2, tracker.UniqueCount);
		Assert.Equal(1, tracker.Duplicates);
		Assert.Equal(0, tracker.Missing);
	}

	[Fact]
	public void Complete_counts_messages_missing_at_end_of_run()
	{
		var tracker = new LossTracker();
		tracker.Record(1);
		tracker.Record(2);

		tracker.Complete(5);

		Assert.Equal(3, tracker.Missing);
		Assert.Equal(2, tracker.UniqueCount);

		tracker.Complete(5);
		Assert.Equal(3, tracker.Missing);
	}

	[Fact]
	public void First_received_message_reveals_leading_loss()
	{
		var tracker = new LossTracker();

		tracker.Record(3);

		Assert.Equal(3, tracker.First);
		Assert.Equal(3, tracker.Last);
		Assert.Equal(2, tracker.Missing);
	}

	[Fact]
	public void Counts_gaps_duplicates_and_out_of_order_messages()
	{
		var tracker = new LossTracker();

		foreach (var id in new long[] { 1, 2, 3, 7, 7, 6 })
			tracker.Record(id);

		Assert.Equal(2, tracker.Missing);
		Assert.Equal(1, tracker.Duplicates);
		Assert.Equal(1, tracker.OutOfOrder);
	}

	[Fact]
	public void Rejects_invalid_message_ids()
	{
		var tracker = new LossTracker();

		Assert.Throws<ArgumentOutOfRangeException>(() => tracker.Record(0));
		Assert.Throws<ArgumentOutOfRangeException>(() => tracker.Complete(-1));
	}
}
