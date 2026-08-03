using System.Diagnostics;
using System.Net.Http.Json;
using LoadGenerator.Cli;
using LoadGenerator.Clients;

namespace LoadGenerator.Services;

public sealed class BenchmarkRunner(BenchmarkOptions options)
{
	private readonly HttpClient _http = new() { Timeout = Timeout.InfiniteTimeSpan };

	public async Task RunAsync()
	{
		var dir = Path.Combine(options.OutputDir, options.RunId);
		Directory.CreateDirectory(dir);
		await ResultWriter.WriteJsonAsync(Path.Combine(dir, "config.json"), options);

		using var setupCts = new CancellationTokenSource(TimeSpan.FromSeconds(options.SetupTimeoutSeconds));
		await WaitForHealthAsync(setupCts.Token);

		var clients = Enumerable.Range(1, options.Clients).Select(CreateClient).ToArray();
		Console.WriteLine($"Connecting {options.Clients} {options.Protocol} clients...");
		await Task.WhenAll(clients.Select(c => c.ConnectAsync(setupCts.Token)));
		if (options.WarmupSeconds > 0)
			await Task.Delay(TimeSpan.FromSeconds(options.WarmupSeconds), setupCts.Token);

		var config = new
		{
			runId = options.RunId,
			payloadSizeBytes = options.PayloadSize,
			messageRatePerSecond = options.Rate,
			durationSeconds = options.Duration,
			totalMessages = options.TotalMessages,
			messageBufferSize = 10000
		};
		using (var start = await _http.PostAsJsonAsync(options.ServerUrl.TrimEnd('/') + "/control/start", config))
			start.EnsureSuccessStatusCode();

		long received = 0, errors = 0;
		using var runCts = new CancellationTokenSource();
		var tasks = clients.Select(c => Task.Run(async () =>
		{
			try
			{
				await c.RunAsync((_, _, _, _) =>
				{
					Interlocked.Increment(ref received);
					return Task.CompletedTask;
				}, runCts.Token);
			}
			catch (OperationCanceledException) { }
			catch (Exception ex)
			{
				Interlocked.Increment(ref errors);
				Console.Error.WriteLine($"Client {c.Id}: {ex.Message}");
			}
		})).ToArray();

		var limitDescription = options.Duration > 0 && options.TotalMessages is not null
			? $"{options.Duration}s or {options.TotalMessages} messages"
			: options.Duration > 0
				? $"{options.Duration}s"
				: $"{options.TotalMessages} messages";
		Console.WriteLine($"Run {options.RunId} started for {limitDescription}");
		var stopwatch = Stopwatch.StartNew();
		while (options.Duration <= 0 || stopwatch.Elapsed < TimeSpan.FromSeconds(options.Duration))
		{
			await Task.Delay(1000);
			Console.WriteLine($"  {stopwatch.Elapsed.TotalSeconds:F0}s: {Interlocked.Read(ref received)} deliveries");
			if (options.TotalMessages is not null)
			{
				var current = await _http.GetFromJsonAsync<ServerStats>(options.ServerUrl.TrimEnd('/') + "/stats");
				if (current?.MessagesGenerated >= options.TotalMessages)
					break;
			}
		}

		using var stop = await _http.PostAsync(options.ServerUrl.TrimEnd('/') + "/control/stop", null);
		stop.EnsureSuccessStatusCode();
		runCts.Cancel();
		try
		{
			await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(5));
		}
		catch (TimeoutException) { }
		if (options.CooldownSeconds > 0)
			await Task.Delay(TimeSpan.FromSeconds(options.CooldownSeconds));
		foreach (var client in clients)
			await client.DisposeAsync();

		Console.WriteLine($"Completed: {Interlocked.Read(ref received)} deliveries, {Interlocked.Read(ref errors)} client errors");
	}

	private IBenchmarkClient CreateClient(int id) => options.Protocol switch
	{
		"ws" => new WebSocketBenchmarkClient(id, options.ServerUrl),
		"sse" => new SseBenchmarkClient(id, options.ServerUrl, _http),
		_ => new LongPollingBenchmarkClient(id, options.ServerUrl, _http, options.LongPollTimeoutMs, options.LongPollMaxBatch)
	};

	private async Task WaitForHealthAsync(CancellationToken token)
	{
		Exception? last = null;
		while (!token.IsCancellationRequested)
		{
			try
			{
				using var response = await _http.GetAsync(options.ServerUrl.TrimEnd('/') + "/health", token);
				if (response.IsSuccessStatusCode)
					return;
				last = new HttpRequestException($"Health endpoint returned {(int)response.StatusCode}.");
			}
			catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
			{
				last = ex;
			}

			await Task.Delay(250, token);
		}

		throw new InvalidOperationException("Benchmark server did not become healthy.", last);
	}

	private sealed class ServerStats
	{
		public long MessagesGenerated { get; set; }
	}
}
