using System.Diagnostics;
using System.Text.Json;
using LoadGenerator.Models;
namespace LoadGenerator.Services;

public static class ClockSynchronizer
{
	public static async Task<ClockSyncResult> SynchronizeAsync(HttpClient http, string url, int samples, CancellationToken t)
	{
		var offsets = new List<double>();
		var rtts = new List<double>();
		for (var i = 0; i < samples; i++)
		{
			var before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			var sw = Stopwatch.StartNew();
			using var r = await http.GetAsync(url.TrimEnd('/') + "/time", t);
			r.EnsureSuccessStatusCode();
			var json = await JsonDocument.ParseAsync(await r.Content.ReadAsStreamAsync(t), cancellationToken: t);
			var after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			var server = json.RootElement.GetProperty("server_time_ms").GetInt64();
			rtts.Add(sw.Elapsed.TotalMilliseconds);
			offsets.Add(server - (before + after) / 2.0);
		}
		var best = Enumerable.Range(0, rtts.Count).OrderBy(i => rtts[i]).Take(Math.Max(1, samples / 2)).Select(i => offsets[i]).Average();
		return new(best, rtts.Average(), rtts.Min(), samples);
	}
}
