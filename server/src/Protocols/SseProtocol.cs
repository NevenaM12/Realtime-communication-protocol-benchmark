using System.Text.Json;
using System.Threading.Channels;
using BenchmarkServer.Models;
using BenchmarkServer.Services;
namespace BenchmarkServer.Protocols;

public static class SseProtocol
{
	public static void MapSseProtocol(this WebApplication app) => app.MapGet("/sse", async (HttpContext ctx, BenchmarkState state) =>
	{
		ctx.Response.Headers.ContentType = "text/event-stream";
		ctx.Response.Headers.CacheControl = "no-cache";
		ctx.Response.Headers.Connection = "keep-alive";
		ctx.Response.Headers["X-Accel-Buffering"] = "no";
		await ctx.Response.StartAsync(ctx.RequestAborted);
		await ctx.Response.WriteAsync(": connected\n\n", ctx.RequestAborted);
		await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
		var id = Guid.NewGuid();
		var ch = Channel.CreateBounded<BenchmarkMessage>(new BoundedChannelOptions(4096)
		{
			FullMode = BoundedChannelFullMode.Wait,
			SingleReader = true
		});
		state.SseClients[id] = ch;
		try
		{
			await foreach (var m in ch.Reader.ReadAllAsync(ctx.RequestAborted))
			{
				var json = JsonSerializer.Serialize(m);
				var evt = $"id: {m.Id}\nevent: message\ndata: {json}\n\n";
				var before = Environment.TickCount64;
				await ctx.Response.WriteAsync(evt, ctx.RequestAborted);
				await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
				if (Environment.TickCount64 - before > 100)
					Interlocked.Increment(ref state.BackpressureEvents);
			}
		}
		catch (OperationCanceledException) { }
		catch (IOException)
		{
			Interlocked.Increment(ref state.SseSendErrors);
		}
		finally
		{
			state.SseClients.TryRemove(id, out _);
		}
	});
}
