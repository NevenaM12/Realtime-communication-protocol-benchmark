using BenchmarkServer.Models;
using BenchmarkServer.Services;

namespace BenchmarkServer.Protocols;

public static class LongPollingProtocol
{
	public static void MapLongPollingProtocol(this WebApplication app) =>
		app.MapGet(
			"/lp",
			async (long lastId, int? timeoutMs, int? maxBatch, HttpContext ctx, BenchmarkState state) =>
		{
			Interlocked.Increment(ref state.TotalPollRequests);
			Interlocked.Increment(ref state.PendingLongPollRequests);
			try
			{
				var result = state.Buffer.ReadAfter(lastId, maxBatch ?? 100);
				if (result.Messages.Count == 0 && (timeoutMs ?? 30000) > 0)
				{
					using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted);
					timeout.CancelAfter(Math.Clamp(timeoutMs ?? 30000, 1, 120000));
					try
					{
						await state.Buffer.WaitForChangeAsync(timeout.Token);
					}
					catch (OperationCanceledException) when (!ctx.RequestAborted.IsCancellationRequested)
					{
						Interlocked.Increment(ref state.LongPollTimeouts);
					}

					result = state.Buffer.ReadAfter(lastId, maxBatch ?? 100);
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
