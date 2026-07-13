namespace BenchmarkServer.Models;

public sealed record ServerStats(
	string? ActiveRunId,
	long MessagesGenerated,
	int ConnectedWebSocketClients,
	int ConnectedSseClients,
	int PendingLongPollRequests,
	long WebSocketSendErrors,
	long SseSendErrors,
	long LongPollTimeouts,
	long BackpressureEvents,
	long ResourceSamplesCollected,
	long TotalPollRequests,
	long EmptyPollResponses,
	long TruncatedResponses);
