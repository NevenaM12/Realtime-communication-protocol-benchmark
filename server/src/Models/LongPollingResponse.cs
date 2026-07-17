namespace BenchmarkServer.Models;

public sealed record LongPollingResponse(IReadOnlyList<BenchmarkMessage> Messages, bool Truncated);
