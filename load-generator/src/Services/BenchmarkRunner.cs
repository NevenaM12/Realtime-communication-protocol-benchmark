using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using LoadGenerator.Cli;
using LoadGenerator.Clients;
using LoadGenerator.Metrics;
using LoadGenerator.Models;

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
		var clock = await ClockSynchronizer.SynchronizeAsync(_http, options.ServerUrl, options.ClockSyncSamples, setupCts.Token);
		await ResultWriter.WriteJsonAsync(Path.Combine(dir, "clock_sync.json"), clock);

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

		var trackers = new ConcurrentDictionary<int, LossTracker>(clients.Select(c => new KeyValuePair<int, LossTracker>(c.Id, new())));
		var clientRunStates = clients.ToDictionary(c => c.Id, _ => new ClientRunState());
		var latency = new LatencyHistogram();
		var bytes = new ByteAccounting();
		long received = 0, errors = 0;
		var benchmarkEnding = 0;
		await using var writer = new ResultWriter(dir, options.RawLog, options.RawLogLimit);
		using var runCts = new CancellationTokenSource();
		var tasks = clients.Select(c => Task.Run(async () =>
		{
			var clientRunState = clientRunStates[c.Id];
			try
			{
				await c.RunAsync(async (id, message, encoded, receivedAt) =>
				{
					var raw = receivedAt - message.SentAt;
					var adjusted = raw + clock.EstimatedClockOffsetMs;
					trackers[id].Record(message.Id);
					latency.Record(adjusted);
					Interlocked.Increment(ref received);
					var protocolBytes = options.Protocol switch
					{
						"ws" => encoded + (encoded < 126 ? 2 : encoded <= 65535 ? 4 : 10),
						"sse" => encoded,
						_ => 0
					};
					bytes.Add(message.Payload.Length, encoded, protocolBytes);
					if (options.RawLog)
						await writer.RawAsync(new
						{
							protocol = options.Protocol,
							clientId = id,
							messageId = message.Id,
							sent_at = message.SentAt,
							received_at = receivedAt,
							adjusted_latency_ms = adjusted,
							raw_latency_ms = raw
						});
				}, runCts.Token);

				if (Volatile.Read(ref benchmarkEnding) == 0)
					Interlocked.Increment(ref clientRunState.Disconnects);
			}
			catch (OperationCanceledException) when (
				runCts.IsCancellationRequested || Volatile.Read(ref benchmarkEnding) != 0) { }
			catch (Exception) when (Volatile.Read(ref benchmarkEnding) != 0) { }
			catch (Exception ex)
			{
				Interlocked.Increment(ref errors);
				Interlocked.Increment(ref clientRunState.Errors);
				Interlocked.Increment(ref clientRunState.Disconnects);
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
		stopwatch.Stop();
		var measuredDurationSeconds = stopwatch.Elapsed.TotalSeconds;

		Volatile.Write(ref benchmarkEnding, 1);
		using var stop = await _http.PostAsync(options.ServerUrl.TrimEnd('/') + "/control/stop", null);
		stop.EnsureSuccessStatusCode();
		var finalJson = await stop.Content.ReadAsStringAsync();
		var final = JsonSerializer.Deserialize<ServerStats>(
			finalJson,
			new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
		if (options.CooldownSeconds > 0)
			await Task.Delay(TimeSpan.FromSeconds(options.CooldownSeconds));
		runCts.Cancel();
		try
		{
			await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(5));
		}
		catch (TimeoutException) { }
		foreach (var client in clients)
			await client.DisposeAsync();

		foreach (var tracker in trackers.Values)
			tracker.Complete(final.MessagesGenerated);
		var clientMetrics = clients
			.OrderBy(c => c.Id)
			.Select(c =>
			{
				var tracker = trackers[c.Id];
				var runState = clientRunStates[c.Id];
				return new ClientMetric(
					c.Id,
					tracker.Count,
					tracker.First,
					tracker.Last,
					tracker.Missing,
					tracker.Duplicates,
					tracker.OutOfOrder,
					Interlocked.Read(ref runState.Disconnects),
					Interlocked.Read(ref runState.Errors),
					c.SetupTimeMs);
			})
			.ToArray();
		var loss = trackers.Values.Sum(x => x.Missing);
		var uniqueReceived = trackers.Values.Sum(x => x.UniqueCount);
		var theoretical = final.MessagesGenerated * options.Clients;
		var pollRequests = clients.Sum(c => c.PollRequests);
		var estimatedProtocolBytes = options.Protocol == "lp"
			? clients.Sum(c => c.ResponseBodyBytes) + pollRequests * 300
			: bytes.EstimatedProtocolBytes;
		var estimatedOverheadBytes = Math.Max(0, estimatedProtocolBytes - bytes.PayloadBytes);
		var summary = new BenchmarkSummary
		{
			RunId = options.RunId,
			Protocol = options.Protocol,
			Clients = options.Clients,
			PayloadSizeBytes = options.PayloadSize,
			MessageRatePerSecond = options.Rate,
			DurationSeconds = options.Duration,
			MessagesReceived = received,
			UniqueMessagesReceived = uniqueReceived,
			MessagesGeneratedByServer = final.MessagesGenerated,
			TheoreticalDeliveries = theoretical,
			DeliveryRatio = theoretical == 0 ? 0 : uniqueReceived / (double)theoretical,
			ThroughputMessagesPerSecond = ThroughputTracker.Calculate(received, measuredDurationSeconds),
			LatencyAvgMs = latency.Average,
			LatencyMedianMs = latency.Percentile(.5),
			LatencyP95Ms = latency.Percentile(.95),
			LatencyP99Ms = latency.Percentile(.99),
			LatencyMinMs = latency.Min,
			LatencyMaxMs = latency.Max,
			ConnectionSetupAvgMs = SetupTimeTracker.Average(clients.Select(c => c.SetupTimeMs)),
			ConnectionSetupP95Ms = SetupTimeTracker.P95(clients.Select(c => c.SetupTimeMs)),
			MessageLossCount = loss,
			MessageLossRate = theoretical == 0 ? 0 : loss / (double)theoretical,
			DuplicateMessageCount = trackers.Values.Sum(x => x.Duplicates),
			OutOfOrderMessageCount = trackers.Values.Sum(x => x.OutOfOrder),
			ErrorCount = errors,
			EstimatedClockOffsetMs = clock.EstimatedClockOffsetMs,
			ClockSyncRttAvgMs = clock.RttAvgMs,
			PayloadBytesDelivered = bytes.PayloadBytes,
			EncodedMessageBytes = bytes.EncodedMessageBytes,
			EstimatedProtocolBytes = estimatedProtocolBytes,
			EstimatedOverheadBytes = estimatedOverheadBytes,
			OverheadRatio = bytes.PayloadBytes == 0 ? 0 : estimatedOverheadBytes / (double)bytes.PayloadBytes,
			PollRequests = pollRequests,
			EmptyPollResponses = clients.Sum(c => c.EmptyPollResponses)
		};
		await File.WriteAllTextAsync(Path.Combine(dir, "server_final_stats.json"), finalJson);
		await ResultWriter.WriteJsonAsync(Path.Combine(dir, "client_metrics.json"), clientMetrics);
		await ResultWriter.WriteJsonAsync(Path.Combine(dir, "final_summary.json"), summary);
		await ResultWriter.WriteSummaryCsvAsync(Path.Combine(dir, "final_summary.csv"), summary);
		Console.WriteLine($"Completed: {received} deliveries, p95={summary.LatencyP95Ms:F2}ms, ratio={summary.DeliveryRatio:P2}");
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

	private sealed class ClientRunState
	{
		public long Disconnects;
		public long Errors;
	}
}
