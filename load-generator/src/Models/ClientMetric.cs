namespace LoadGenerator.Models;

public sealed record ClientMetric(
	int ClientId,
	long MessagesReceived,
	long FirstMessageId,
	long LastMessageId,
	long MissingMessageCount,
	long DuplicateMessageCount,
	long OutOfOrderMessageCount,
	long DisconnectCount,
	long ErrorCount,
	double SetupTimeMs);
