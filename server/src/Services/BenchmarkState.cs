using System.Collections.Concurrent;
using System.Threading.Channels;
using BenchmarkServer.Models;

namespace BenchmarkServer.Services;

public sealed class BenchmarkState
{
	private readonly object _gate = new();
	private CancellationTokenSource? _runCts;

	public BenchmarkRunConfig? Config { get; private set; }
	public MessageBuffer Buffer { get; } = new();
	public ConcurrentDictionary<Guid, Channel<BenchmarkMessage>> WebSockets { get; } = new();
	public ConcurrentDictionary<Guid, Channel<BenchmarkMessage>> SseClients { get; } = new();
	public long MessagesGenerated;
	public long WebSocketSendErrors;
	public long SseSendErrors;
	public long LongPollTimeouts;
	public long BackpressureEvents;
	public long ResourceSamplesCollected;
	public long TotalPollRequests;
	public long EmptyPollResponses;
	public long TruncatedResponses;
	public int PendingLongPollRequests;

	public CancellationToken Start(BenchmarkRunConfig config)
	{
		lock (_gate)
		{
			if (_runCts is not null)
				throw new InvalidOperationException("A benchmark run is already active.");
			config.Validate();
			Config = config;
			Buffer.Reset(config.MessageBufferSize);
			ResetCounters();
			_runCts = new();
			return _runCts.Token;
		}
	}

	public void Stop()
	{
		lock (_gate)
		{
			_runCts?.Cancel();
			_runCts?.Dispose();
			_runCts = null;
			Config = null;
		}
	}

	public bool Active
	{
		get
		{
			lock (_gate)
				return _runCts is not null;
		}
	}

	private void ResetCounters()
	{
		MessagesGenerated =
			WebSocketSendErrors =
			SseSendErrors =
			LongPollTimeouts =
			BackpressureEvents =
			ResourceSamplesCollected =
			TotalPollRequests =
			EmptyPollResponses =
			TruncatedResponses = 0;
		PendingLongPollRequests = 0;
	}

	public BenchmarkMessage CreateMessage(long id, int size) => new(
		id,
		DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
		new string('A', size));

	public void Publish(BenchmarkMessage m)
	{
		Buffer.Append(m);
		Interlocked.Exchange(ref MessagesGenerated, m.Id);
		PublishTo(WebSockets, m);
		PublishTo(SseClients, m);
	}

	private void PublishTo(ConcurrentDictionary<Guid, Channel<BenchmarkMessage>> clients, BenchmarkMessage m)
	{
		foreach (var channel in clients.Values)
		{
			if (!channel.Writer.TryWrite(m))
				Interlocked.Increment(ref BackpressureEvents);
		}
	}

	public ServerStats Snapshot(string? runIdOverride = null) => new(
		runIdOverride ?? Config?.RunId,
		Interlocked.Read(ref MessagesGenerated),
		WebSockets.Count,
		SseClients.Count,
		Volatile.Read(ref PendingLongPollRequests),
		Interlocked.Read(ref WebSocketSendErrors),
		Interlocked.Read(ref SseSendErrors),
		Interlocked.Read(ref LongPollTimeouts),
		Interlocked.Read(ref BackpressureEvents),
		Interlocked.Read(ref ResourceSamplesCollected),
		Interlocked.Read(ref TotalPollRequests),
		Interlocked.Read(ref EmptyPollResponses),
		Interlocked.Read(ref TruncatedResponses));
}
