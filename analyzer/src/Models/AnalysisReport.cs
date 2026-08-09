namespace BenchmarkAnalyzer.Models;

public sealed class AnalysisReport
{
	public DateTime GeneratedAtUtc { get; set; }
	public int RunCount { get; set; }
	public IReadOnlyList<ProtocolAggregate> ProtocolAggregates { get; set; } = [];
}
