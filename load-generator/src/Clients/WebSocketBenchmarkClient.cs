using System.Diagnostics;
using System.Net.WebSockets;
using System.Text.Json;
using LoadGenerator.Models;

namespace LoadGenerator.Clients;

public sealed class WebSocketBenchmarkClient(int id, string serverUrl) : IBenchmarkClient
{
	private readonly ClientWebSocket _socket = new();

	public int Id => id;
	public double SetupTimeMs { get; private set; }
	public long PollRequests => 0;
	public long EmptyPollResponses => 0;
	public long ResponseBodyBytes => 0;

	public async Task ConnectAsync(CancellationToken t)
	{
		var sw = Stopwatch.StartNew();
		var uri = new Uri(serverUrl.Replace("http://", "ws://").Replace("https://", "wss://").TrimEnd('/') + "/ws");
		await _socket.ConnectAsync(uri, t);
		SetupTimeMs = sw.Elapsed.TotalMilliseconds;
	}

	public async Task RunAsync(Func<int, BenchmarkMessage, int, long, Task> receive, CancellationToken t)
	{
		var buffer = new byte[1024 * 128];
		while (!t.IsCancellationRequested && _socket.State == WebSocketState.Open)
		{
			using var ms = new MemoryStream();
			WebSocketReceiveResult r;
			do
			{
				r = await _socket.ReceiveAsync(buffer, t);
				if (r.MessageType == WebSocketMessageType.Close)
					return;
				ms.Write(buffer, 0, r.Count);
			} while (!r.EndOfMessage);
			var m = JsonSerializer.Deserialize<BenchmarkMessage>(ms.GetBuffer().AsSpan(0, (int)ms.Length))!;
			await receive(Id, m, (int)ms.Length, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
		}
	}

	public async ValueTask DisposeAsync()
	{
		if (_socket.State == WebSocketState.Open)
			try
			{
				await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
			}
			catch { }
		_socket.Dispose();
	}
}
