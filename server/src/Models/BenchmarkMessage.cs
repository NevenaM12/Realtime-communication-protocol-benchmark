using System.Text.Json.Serialization;
namespace BenchmarkServer.Models;

public sealed record BenchmarkMessage(long Id, [property: JsonPropertyName("sent_at")] long SentAt, string Payload);
