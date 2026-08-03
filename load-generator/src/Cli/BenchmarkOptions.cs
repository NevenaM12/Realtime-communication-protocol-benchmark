namespace LoadGenerator.Cli;

public sealed record BenchmarkOptions(
	string Protocol,
	int Clients,
	int PayloadSize,
	int Rate,
	int Duration,
	long? TotalMessages,
	string RunId,
	string ServerUrl,
	int WarmupSeconds,
	int CooldownSeconds,
	int LongPollTimeoutMs,
	int LongPollMaxBatch,
	string OutputDir,
	bool RawLog,
	int RawLogLimit,
	int SetupTimeoutSeconds,
	int ClockSyncSamples);
