using BenchmarkServer.Models;
using BenchmarkServer.Services;

namespace BenchmarkServer.Protocols;

public static class LongPollingProtocol
{
	private const int DefaultMaxBatch = 100;

	public static void MapLongPollingProtocol(this WebApplication app) =>
		app.MapGet(
			"/lp",
			async (long lastId, int? timeoutMs, int? maxBatch, HttpContext ctx, BenchmarkState state) =>
			{
				var batchSize = maxBatch ?? DefaultMaxBatch;
				if (batchSize <= 0)
					return Results.BadRequest("maxBatch must be positive.");

				Interlocked.Increment(ref state.TotalPollRequests);
				Interlocked.Increment(ref state.PendingLongPollRequests);
				try
				{
					var initial = state.Buffer.ReadAfterAndWatch(lastId, batchSize);
					var result = (initial.Messages, initial.Truncated);
					if (result.Messages.Count == 0 && (timeoutMs ?? 30000) > 0)
					{
						using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted);
						timeout.CancelAfter(Math.Clamp(timeoutMs ?? 30000, 1, 120000));
						try
						{
							await initial.ChangeTask.WaitAsync(timeout.Token);
						}
						catch (OperationCanceledException) when (!ctx.RequestAborted.IsCancellationRequested)
						{
							Interlocked.Increment(ref state.LongPollTimeouts);
						}

						result = state.Buffer.ReadAfter(lastId, batchSize);
					}

					if (result.Messages.Count == 0)
						Interlocked.Increment(ref state.EmptyPollResponses);

					if (result.Truncated)
						Interlocked.Increment(ref state.TruncatedResponses);

					return Results.Json(new LongPollingResponse(result.Messages, result.Truncated));
				}
				finally
				{
					Interlocked.Decrement(ref state.PendingLongPollRequests);
				}
			});
}
