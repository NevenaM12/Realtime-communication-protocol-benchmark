using System.Text.Json.Serialization;

namespace BenchmarkServer.Models;

public sealed record ResourceSample(
	[property: JsonPropertyName("timestamp_ms")] long TimestampMs,
	[property: JsonPropertyName("process_cpu_percent")] double ProcessCpuPercent,
	[property: JsonPropertyName("process_memory_rss_bytes")] long ProcessMemoryRssBytes,
	[property: JsonPropertyName("managed_heap_bytes")] long ManagedHeapBytes,
	[property: JsonPropertyName("gc_total_allocated_bytes")] long GcTotalAllocatedBytes,
	[property: JsonPropertyName("gc_gen0")] int GcGen0,
	[property: JsonPropertyName("gc_gen1")] int GcGen1,
	[property: JsonPropertyName("gc_gen2")] int GcGen2,
	[property: JsonPropertyName("thread_count")] int ThreadCount,
	[property: JsonPropertyName("cgroup_cpu_usage_usec")] long? CgroupCpuUsageUsec,
	[property: JsonPropertyName("cgroup_memory_bytes")] long? CgroupMemoryBytes,
	[property: JsonPropertyName("connected_websocket_clients")] int ConnectedWebSocketClients,
	[property: JsonPropertyName("connected_sse_clients")] int ConnectedSseClients,
	[property: JsonPropertyName("pending_long_poll_requests")] int PendingLongPollRequests,
	[property: JsonPropertyName("messages_generated")] long MessagesGenerated,
	[property: JsonPropertyName("backpressure_events")] long BackpressureEvents);
