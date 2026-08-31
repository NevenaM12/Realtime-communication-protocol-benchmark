using System.Diagnostics;
using BenchmarkServer.Models;
namespace BenchmarkServer.Services;

public sealed class MessageGenerator(BenchmarkState state, ILogger<MessageGenerator> logger)
{
	private Task? _task;
	public Task StartAsync(BenchmarkRunConfig config, CancellationToken token)
	{
		_task = RunAsync(config, token);
		return Task.CompletedTask;
	}
	public async Task WaitAsync()
	{
		if (_task is not null)
			try
			{
				await _task;
			}
			catch (OperationCanceledException) { }
	}
	private async Task RunAsync(BenchmarkRunConfig c, CancellationToken token)
	{
		var sw = Stopwatch.StartNew();
		long generated = 0;
		var max = c.TotalMessages ?? long.MaxValue;
		var hasDurationLimit = c.DurationSeconds > 0;
		try
		{
			while (!token.IsCancellationRequested &&
			       generated < max &&
			       (!hasDurationLimit || sw.Elapsed.TotalSeconds < c.DurationSeconds))
			{
				var target = (long)Math.Floor(sw.Elapsed.TotalSeconds * c.MessageRatePerSecond);
				if (target <= generated)
				{
					await Task.Delay(c.MessageRatePerSecond >= 100 ? 1 : (int)(500 / c.MessageRatePerSecond), token);
					continue;
				}
				var batch = Math.Min(target - generated, 1000);
				for (var i = 0; i < batch && generated < max; i++)
				{
					generated++;
					state.Publish(state.CreateMessage(generated, c.PayloadSizeBytes));
				}
			}
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested) { }
		catch (Exception ex)
		{
			logger.LogError(ex, "Message generator failed");
			throw;
		}
	}
}
