namespace BenchmarkServer.Models;

public sealed record BenchmarkRunConfig(
	string RunId,
	int PayloadSizeBytes,
	double MessageRatePerSecond,
	int DurationSeconds,
	long? TotalMessages,
	int MessageBufferSize = 10000)
{
	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(RunId) || RunId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || RunId.Contains(".."))
			throw new ArgumentException("Invalid runId.");
		if (PayloadSizeBytes < 0)
			throw new ArgumentOutOfRangeException(nameof(PayloadSizeBytes));
		if (MessageRatePerSecond <= 0 || MessageRatePerSecond > 100000)
			throw new ArgumentOutOfRangeException(nameof(MessageRatePerSecond));
		if (DurationSeconds < 0 || (DurationSeconds == 0 && TotalMessages is null))
			throw new ArgumentOutOfRangeException(nameof(DurationSeconds), "Duration must be non-negative and can be zero only when totalMessages is specified.");
		if (TotalMessages <= 0)
			throw new ArgumentOutOfRangeException(nameof(TotalMessages));
		if (MessageBufferSize <= 0)
			throw new ArgumentOutOfRangeException(nameof(MessageBufferSize));
	}
}
