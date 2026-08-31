using System.Text.Json.Serialization;
namespace LoadGenerator.Models;

public sealed record BenchmarkMessage(long Id, [property: JsonPropertyName("created_at")] long CreatedAt, string Payload);
