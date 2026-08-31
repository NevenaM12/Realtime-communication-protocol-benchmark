using LoadGenerator.Cli;
using Xunit;

namespace LoadGenerator.Tests;

public sealed class CommandLineParserTests
{
	[Fact]
	public void Uses_fixed_message_limits_by_default()
	{
		var options = CommandLineParser.Parse(["--protocol", "lp"]);

		Assert.Equal(100, options.LongPollMaxBatch);
		Assert.Equal(4096, options.ClientQueueCapacity);
		Assert.Equal(4096, options.MessageBufferSize);
	}

	[Fact]
	public void Reads_custom_long_poll_batch_size()
	{
		var options = CommandLineParser.Parse([
			"--protocol", "lp",
			"--long-poll-max-batch", "250"
		]);

		Assert.Equal(250, options.LongPollMaxBatch);
	}

	[Fact]
	public void Rejects_non_positive_long_poll_batch_size()
	{
		var error = Assert.Throws<ArgumentException>(() => CommandLineParser.Parse([
			"--protocol", "lp",
			"--long-poll-max-batch", "0"
		]));

		Assert.Contains("long-poll max batch", error.Message);
	}

	[Fact]
	public void Rejects_unknown_options()
	{
		var error = Assert.Throws<ArgumentException>(() => CommandLineParser.Parse([
			"--protocol", "lp",
			"--unknown-option", "value"
		]));

		Assert.Contains("Unknown option", error.Message);
	}
}
