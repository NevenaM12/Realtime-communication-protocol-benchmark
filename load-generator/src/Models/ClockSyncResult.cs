namespace LoadGenerator.Models;

public sealed record ClockSyncResult(
	double EstimatedClockOffsetMs,
	double RttAvgMs,
	double RttMinMs,
	int Samples);
