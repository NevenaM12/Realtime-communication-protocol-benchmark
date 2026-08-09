using System.Text.Json;

namespace BenchmarkAnalyzer.Output;

internal static class JsonWriter
{
	private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

	public static Task WriteAsync<T>(string path, T value) =>
		File.WriteAllTextAsync(path, JsonSerializer.Serialize(value, Json));
}
