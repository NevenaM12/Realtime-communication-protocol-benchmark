using System.Diagnostics;
using System.Text;
using System.Text.Json;
using LoadGenerator.Models;

namespace LoadGenerator.Clients;

public sealed class SseBenchmarkClient(int id, string serverUrl, HttpClient http) : IBenchmarkClient
{
	private HttpResponseMessage? _response;
	private StreamReader? _reader;

	public int Id => id;
	public double SetupTimeMs { get; private set; }
	public long PollRequests => 0;
	public long EmptyPollResponses => 0;
	public long ResponseBodyBytes => 0;

	public async Task ConnectAsync(CancellationToken t)
	{
		var sw = Stopwatch.StartNew();
		_response = await http.GetAsync(serverUrl.TrimEnd('/') + "/sse", HttpCompletionOption.ResponseHeadersRead, t);
		_response.EnsureSuccessStatusCode();
		_reader = new StreamReader(await _response.Content.ReadAsStreamAsync(t));
		SetupTimeMs = sw.Elapsed.TotalMilliseconds;
	}

	public async Task RunAsync(Func<int, BenchmarkMessage, int, long, Task> receive, CancellationToken t)
	{
		while (!t.IsCancellationRequested)
		{
			string? line;
			string? data = null;
			var bytes = 0;
			while ((line = await _reader!.ReadLineAsync(t)) is not null)
			{
				bytes += Encoding.UTF8.GetByteCount(line) + 1;
				if (line.StartsWith("data: "))
					data = line[6..];
				else if (line.Length == 0)
					break;
			}
			if (line is null)
				return;
			if (data is not null)
			{
				var m = JsonSerializer.Deserialize<BenchmarkMessage>(data)!;
				await receive(Id, m, bytes, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
			}
		}
	}

	public ValueTask DisposeAsync()
	{
		_reader?.Dispose();
		_response?.Dispose();
		return ValueTask.CompletedTask;
	}
}
