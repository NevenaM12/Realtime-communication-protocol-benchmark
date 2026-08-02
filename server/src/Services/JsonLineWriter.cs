using System.Text.Json;
namespace BenchmarkServer.Services;

public static class JsonLineWriter
{
	public static Task WriteAsync<T>(TextWriter writer, T value) => writer.WriteLineAsync(JsonSerializer.Serialize(value));
}
