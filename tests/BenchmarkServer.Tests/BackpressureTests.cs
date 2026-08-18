using System.Threading.Channels;
using BenchmarkServer.Models;
using BenchmarkServer.Services;
using Xunit;

namespace BenchmarkServer.Tests;

public sealed class BackpressureTests
{
	[Fact]
	public void Slow_client_does_not_block_fast_client()
	{
		var state = new BenchmarkState();
		state.Start(new BenchmarkRunConfig(
			RunId: "slow-client-test",
			PayloadSizeBytes: 16,
			MessageRatePerSecond: 10,
			DurationSeconds: 0,
			TotalMessages: 3,
			MessageBufferSize: 100));

		try
		{
			var slowClient = CreateChannel(capacity: 1);
			var fastClient = CreateChannel(capacity: 1);
			state.WebSockets[Guid.NewGuid()] = slowClient;
			state.WebSockets[Guid.NewGuid()] = fastClient;

			state.Publish(state.CreateMessage(id: 1, size: 16));

			Assert.True(fastClient.Reader.TryRead(out var fastFirst));
			Assert.Equal(1, fastFirst.Id);

			// The slow client deliberately leaves message 1 in its capacity-one queue.
			state.Publish(state.CreateMessage(id: 2, size: 16));

			Assert.Equal(1, Interlocked.Read(ref state.BackpressureEvents));
			Assert.True(fastClient.Reader.TryRead(out var fastSecond));
			Assert.Equal(2, fastSecond.Id);

			state.Publish(state.CreateMessage(id: 3, size: 16));

			Assert.Equal(2, Interlocked.Read(ref state.BackpressureEvents));
			Assert.True(fastClient.Reader.TryRead(out var fastThird));
			Assert.Equal(3, fastThird.Id);

			Assert.True(slowClient.Reader.TryRead(out var slowOnlyMessage));
			Assert.Equal(1, slowOnlyMessage.Id);
			Assert.False(slowClient.Reader.TryRead(out _));

			var buffered = state.Buffer.ReadAfter(lastId: 0, maxBatch: 10);
			Assert.Equal(new long[] { 1, 2, 3 }, buffered.Messages.Select(message => message.Id));
			Assert.False(buffered.Truncated);
			Assert.Equal(3, state.Snapshot().MessagesGenerated);
		}
		finally
		{
			state.Stop();
		}
	}

	private static Channel<BenchmarkMessage> CreateChannel(int capacity) =>
		Channel.CreateBounded<BenchmarkMessage>(new BoundedChannelOptions(capacity)
		{
			FullMode = BoundedChannelFullMode.Wait,
			SingleReader = true
		});
}
