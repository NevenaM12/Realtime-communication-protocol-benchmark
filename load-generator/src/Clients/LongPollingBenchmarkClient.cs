using System.Diagnostics;
using System.Text.Json;
using LoadGenerator.Models;

namespace LoadGenerator.Clients;

public sealed class LongPollingBenchmarkClient(int id, string serverUrl, HttpClient http, int timeoutMs, int maxBatch) : IBenchmarkClient
{
	private long _lastId;
	private long _polls;
	private long _empty;
	private long _responseBodyBytes;

	public int Id => id;
	public double SetupTimeMs { get; private set; }
	public long PollRequests => _polls;
	public long EmptyPollResponses => _empty;
	public long ResponseBodyBytes => _responseBodyBytes;

	public async Task ConnectAsync(CancellationToken t)
	{
		var sw = Stopwatch.StartNew();
		using var r = await http.GetAsync($"{serverUrl.TrimEnd('/')}/lp?lastId=0&timeoutMs=0&maxBatch={maxBatch}", t);
		r.EnsureSuccessStatusCode();
		SetupTimeMs = sw.Elapsed.TotalMilliseconds;
	}

	public async Task RunAsync(Func<int, BenchmarkMessage, int, long, Task> receive, CancellationToken t)
	{
		while (!t.IsCancellationRequested)
		{
			var url = $"{serverUrl.TrimEnd('/')}/lp?lastId={_lastId}&timeoutMs={timeoutMs}&maxBatch={maxBatch}";
			using var r = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, t);
			r.EnsureSuccessStatusCode();
			var bytes = await r.Content.ReadAsByteArrayAsync(t);
			Interlocked.Increment(ref _polls);
			Interlocked.Add(ref _responseBodyBytes, bytes.Length);
			var body = JsonSerializer.Deserialize<Response>(bytes, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
			if (body.Messages.Count == 0)
				Interlocked.Increment(ref _empty);
			foreach (var m in body.Messages)
			{
				_lastId = Math.Max(_lastId, m.Id);
				await receive(Id, m, bytes.Length / body.Messages.Count, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
			}
		}
	}

	public ValueTask DisposeAsync() => ValueTask.CompletedTask;

	private sealed record Response(List<BenchmarkMessage> Messages, bool Truncated);
}
