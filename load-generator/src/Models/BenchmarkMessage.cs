using System.Text.Json.Serialization;
namespace LoadGenerator.Models;

public sealed record BenchmarkMessage(long Id, [property: JsonPropertyName("sent_at")] long SentAt, string Payload);
