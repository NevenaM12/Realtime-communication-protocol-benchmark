using System.Globalization;
using System.Text.Json;
using LoadGenerator.Metrics;
using LoadGenerator.Models;

namespace LoadGenerator.Services;

public sealed class ResultWriter(string dir, bool raw, int limit) : IAsyncDisposable
{
	private readonly SemaphoreSlim _gate = new(1, 1);
	private readonly StreamWriter? _raw = raw
		? new StreamWriter(Path.Combine(dir, "client_messages.jsonl"), false)
		: null;
	private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
	private int _rawCount;

	public static Task WriteJsonAsync<T>(string path, T value) =>
		File.WriteAllTextAsync(path, JsonSerializer.Serialize(value, Json));

	public async Task RawAsync(object value)
	{
		if (_raw is null || Interlocked.Increment(ref _rawCount) > limit)
			return;
		await _gate.WaitAsync();
		try
		{
			await _raw.WriteLineAsync(JsonSerializer.Serialize(value));
		}
		finally
		{
			_gate.Release();
		}
	}

	public static async Task WriteSummaryCsvAsync(string path, BenchmarkSummary s)
	{
		var props = typeof(BenchmarkSummary).GetProperties();
		await using var w = new StreamWriter(path);
		await w.WriteLineAsync(string.Join(',', props.Select(p => p.Name)));
		await w.WriteLineAsync(string.Join(',', props.Select(p => Convert.ToString(p.GetValue(s), CultureInfo.InvariantCulture))));
	}

	public async ValueTask DisposeAsync()
	{
		if (_raw is not null)
			await _raw.DisposeAsync();
		_gate.Dispose();
	}
}
