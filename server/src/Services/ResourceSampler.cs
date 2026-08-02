using System.Diagnostics;
using System.Text.Json;
using BenchmarkServer.Models;
using BenchmarkServer.Utilities;

namespace BenchmarkServer.Services;

public sealed class ResourceSampler(BenchmarkState state)
{
	private Task? _task;

	public void Start(string runId, CancellationToken token)
	{
		_task = RunAsync(runId, token);
	}

	public async Task StopAsync()
	{
		if (_task is not null)
			try
			{
				await _task;
			}
			catch (OperationCanceledException) { }
	}

	private async Task RunAsync(string runId, CancellationToken token)
	{
		var dir = Path.Combine(Environment.GetEnvironmentVariable("RESULTS_DIR") ?? "/app/results", runId);
		Directory.CreateDirectory(dir);
		await using var writer = new StreamWriter(Path.Combine(dir, "server_resources.jsonl"), false);
		var process = Process.GetCurrentProcess();
		var oldCpu = process.TotalProcessorTime;
		var oldAt = Stopwatch.GetTimestamp();
		using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
		while (await timer.WaitForNextTickAsync(token))
		{
			process.Refresh();
			var now = Stopwatch.GetTimestamp();
			var cpu = process.TotalProcessorTime;
			var elapsed = (now - oldAt) / (double)Stopwatch.Frequency;
			var pct = (cpu - oldCpu).TotalSeconds / elapsed / Environment.ProcessorCount * 100;
			oldCpu = cpu;
			oldAt = now;
			var s = new ResourceSample(
				DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
				pct,
				process.WorkingSet64,
				GC.GetTotalMemory(false),
				GC.GetTotalAllocatedBytes(),
				GC.CollectionCount(0),
				GC.CollectionCount(1),
				GC.CollectionCount(2),
				process.Threads.Count,
				CgroupReader.ReadCpuUsageUsec(),
				CgroupReader.ReadMemory(),
				state.WebSockets.Count,
				state.SseClients.Count,
				state.PendingLongPollRequests,
				state.MessagesGenerated,
				state.BackpressureEvents);
			await writer.WriteLineAsync(JsonSerializer.Serialize(s));
			await writer.FlushAsync(token);
			Interlocked.Increment(ref state.ResourceSamplesCollected);
		}
	}
}
