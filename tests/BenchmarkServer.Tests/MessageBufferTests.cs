using BenchmarkServer.Models;
using BenchmarkServer.Services;
using Xunit;

namespace BenchmarkServer.Tests;

public class MessageBufferTests
{
	[Fact]
	public void Evicts_the_oldest_messages_when_capacity_is_exceeded()
	{
		var buffer = new MessageBuffer();
		buffer.Reset(capacity: 3);
		for (var id = 1; id <= 5; id++)
			buffer.Append(new BenchmarkMessage(id, id, "payload"));

		var result = buffer.ReadAfter(lastId: 0, maxBatch: 10);

		Assert.Equal(new long[] { 3, 4, 5 }, result.Messages.Select(message => message.Id));
		Assert.True(result.Truncated);
	}

	[Fact]
	public void Returns_only_the_requested_number_of_messages()
	{
		var buffer = new MessageBuffer();
		buffer.Reset(capacity: 10);
		for (var id = 1; id <= 5; id++)
			buffer.Append(new BenchmarkMessage(id, id, "payload"));

		var result = buffer.ReadAfter(lastId: 1, maxBatch: 2);

		Assert.Equal(new long[] { 2, 3 }, result.Messages.Select(message => message.Id));
		Assert.False(result.Truncated);
	}
}
