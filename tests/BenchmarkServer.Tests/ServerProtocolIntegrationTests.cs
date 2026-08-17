using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using BenchmarkServer.Models;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace BenchmarkServer.Tests;

public sealed class ServerProtocolIntegrationTests
{
	private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
	{
		PropertyNameCaseInsensitive = true
	};

	[Fact]
	public async Task Health_and_time_endpoints_are_available()
	{
		await using var host = await ServerTestHost.StartAsync();
		using var http = host.CreateHttpClient();

		using var health = await http.GetAsync("/health");
		health.EnsureSuccessStatusCode();
		var healthJson = await health.Content.ReadFromJsonAsync<JsonElement>();
		Assert.True(healthJson.GetProperty("ok").GetBoolean());

		using var time = await http.GetAsync("/time");
		time.EnsureSuccessStatusCode();
		var timeJson = await time.Content.ReadFromJsonAsync<JsonElement>();
		Assert.True(timeJson.GetProperty("server_time_ms").GetInt64() > 0);
	}

	[Theory]
	[InlineData("ws")]
	[InlineData("sse")]
	[InlineData("lp")]
	public async Task Protocol_receives_message_during_controlled_run(string protocol)
	{
		await using var host = await ServerTestHost.StartAsync();
		using var http = host.CreateHttpClient();
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		var runId = $"integration-{protocol}-{Guid.NewGuid():N}";

		var message = protocol switch
		{
			"ws" => await ReceiveWebSocketMessageAsync(host, http, runId, timeout.Token),
			"sse" => await ReceiveSseMessageAsync(http, runId, timeout.Token),
			_ => await ReceiveLongPollingMessageAsync(http, runId, timeout.Token)
		};

		Assert.Equal(1, message.Id);
		Assert.Equal(new string('A', 32), message.Payload);
		Assert.True(message.SentAt > 0);
		Assert.True(File.Exists(Path.Combine(host.ResultsDirectory, runId, "server_config.json")));
		Assert.True(File.Exists(Path.Combine(host.ResultsDirectory, runId, "server_final_stats.json")));
	}

	private static async Task<BenchmarkMessage> ReceiveWebSocketMessageAsync(
		ServerTestHost host,
		HttpClient http,
		string runId,
		CancellationToken token)
	{
		using var socket = new ClientWebSocket();
		var uri = new UriBuilder(host.BaseAddress)
		{
			Scheme = host.BaseAddress.Scheme == "https" ? "wss" : "ws",
			Path = "/ws"
		}.Uri;
		await socket.ConnectAsync(uri, token);
		await WaitForClientAsync(http, stats => stats.ConnectedWebSocketClients == 1, token);

		await StartRunAsync(http, runId, token);
		try
		{
			var buffer = new byte[4096];
			using var payload = new MemoryStream();
			WebSocketReceiveResult result;
			do
			{
				result = await socket.ReceiveAsync(buffer, token);
				payload.Write(buffer, 0, result.Count);
			} while (!result.EndOfMessage);

			return JsonSerializer.Deserialize<BenchmarkMessage>(payload.ToArray(), Json)
				?? throw new InvalidOperationException("The WebSocket message was empty.");
		}
		finally
		{
			await StopRunAsync(http, runId, token);
			socket.Abort();
		}
	}

	private static async Task<BenchmarkMessage> ReceiveSseMessageAsync(
		HttpClient http,
		string runId,
		CancellationToken token)
	{
		using var response = await http.GetAsync("/sse", HttpCompletionOption.ResponseHeadersRead, token);
		response.EnsureSuccessStatusCode();
		await WaitForClientAsync(http, stats => stats.ConnectedSseClients == 1, token);
		await using var stream = await response.Content.ReadAsStreamAsync(token);
		using var reader = new StreamReader(stream, Encoding.UTF8);

		await StartRunAsync(http, runId, token);
		try
		{
			while (await reader.ReadLineAsync(token) is { } line)
			{
				if (!line.StartsWith("data: ", StringComparison.Ordinal))
					continue;

				return JsonSerializer.Deserialize<BenchmarkMessage>(line[6..], Json)
					?? throw new InvalidOperationException("The SSE message was empty.");
			}

			throw new InvalidOperationException("The SSE stream ended before a message was received.");
		}
		finally
		{
			await StopRunAsync(http, runId, token);
		}
	}

	private static async Task<BenchmarkMessage> ReceiveLongPollingMessageAsync(
		HttpClient http,
		string runId,
		CancellationToken token)
	{
		using (var ready = await http.GetAsync("/lp?lastId=0&timeoutMs=0&maxBatch=10", token))
			ready.EnsureSuccessStatusCode();

		await StartRunAsync(http, runId, token);
		try
		{
			using var response = await http.GetAsync("/lp?lastId=0&timeoutMs=5000&maxBatch=10", token);
			response.EnsureSuccessStatusCode();
			var body = await response.Content.ReadFromJsonAsync<LongPollingResponse>(Json, token)
				?? throw new InvalidOperationException("The Long Polling response was empty.");
			return Assert.Single(body.Messages);
		}
		finally
		{
			await StopRunAsync(http, runId, token);
		}
	}

	private static async Task StartRunAsync(HttpClient http, string runId, CancellationToken token)
	{
		var config = new BenchmarkRunConfig(
			runId,
			PayloadSizeBytes: 32,
			MessageRatePerSecond: 5,
			DurationSeconds: 0,
			TotalMessages: 1,
			MessageBufferSize: 100);
		using var response = await http.PostAsJsonAsync("/control/start", config, token);
		response.EnsureSuccessStatusCode();
	}

	private static async Task StopRunAsync(
		HttpClient http,
		string runId,
		CancellationToken token)
	{
		using var response = await http.PostAsync("/control/stop", null, token);
		response.EnsureSuccessStatusCode();
		var stats = await response.Content.ReadFromJsonAsync<ServerStats>(Json, token)
			?? throw new InvalidOperationException("The stop response did not contain server statistics.");
		Assert.Equal(runId, stats.ActiveRunId);
		Assert.Equal(1, stats.MessagesGenerated);
	}

	private static async Task WaitForClientAsync(
		HttpClient http,
		Func<ServerStats, bool> condition,
		CancellationToken token)
	{
		while (true)
		{
			var stats = await http.GetFromJsonAsync<ServerStats>("/stats", Json, token)
				?? throw new InvalidOperationException("The stats endpoint returned no data.");
			if (condition(stats))
				return;
			await Task.Delay(10, token);
		}
	}
}
