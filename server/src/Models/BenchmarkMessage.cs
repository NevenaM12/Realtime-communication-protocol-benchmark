using System.Text.Json.Serialization;
namespace BenchmarkServer.Models;

public sealed record BenchmarkMessage(long Id, [property: JsonPropertyName("created_at")] long CreatedAt, string Payload);
