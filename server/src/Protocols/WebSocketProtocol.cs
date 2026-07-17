using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Channels;
using BenchmarkServer.Models;
using BenchmarkServer.Services;
namespace BenchmarkServer.Protocols;

public static class WebSocketProtocol
{
	public static void MapWebSocketProtocol(this WebApplication app) => app.MapGet("/ws", async (HttpContext ctx, BenchmarkState state) =>
	{
		if (!ctx.WebSockets.IsWebSocketRequest)
		{
			ctx.Response.StatusCode = 400;
			return;
		}
		using var socket = await ctx.WebSockets.AcceptWebSocketAsync();
		var id = Guid.NewGuid();
		var ch = Channel.CreateBounded<BenchmarkMessage>(new BoundedChannelOptions(4096)
		{
			FullMode = BoundedChannelFullMode.Wait,
			SingleReader = true
		});
		state.WebSockets[id] = ch;
		try
		{
			await foreach (var m in ch.Reader.ReadAllAsync(ctx.RequestAborted))
			{
				var bytes = JsonSerializer.SerializeToUtf8Bytes(m);
				var before = Environment.TickCount64;
				await socket.SendAsync(bytes, WebSocketMessageType.Text, true, ctx.RequestAborted);
				if (Environment.TickCount64 - before > 100)
					Interlocked.Increment(ref state.BackpressureEvents);
			}
		}
		catch (OperationCanceledException) { }
		catch (WebSocketException)
		{
			Interlocked.Increment(ref state.WebSocketSendErrors);
		}
		finally
		{
			state.WebSockets.TryRemove(id, out _);
			if (socket.State == WebSocketState.Open)
			{
				try
				{
					await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "benchmark ended", CancellationToken.None);
				}
				catch (WebSocketException)
				{
					// The client may close its transport before completing the close handshake.
				}
			}
		}
	});
}
