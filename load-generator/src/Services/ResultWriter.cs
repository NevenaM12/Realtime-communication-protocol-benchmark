using System.Text.Json;

namespace LoadGenerator.Services;

public static class ResultWriter
{
	private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

	public static Task WriteJsonAsync<T>(string path, T value) =>
		File.WriteAllTextAsync(path, JsonSerializer.Serialize(value, Json));
}
