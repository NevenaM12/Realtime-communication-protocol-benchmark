using System.Text;
namespace BenchmarkServer.Services;

public static class ByteAccounting
{
	public static int WebSocketFrameBytes(int jsonBytes) => jsonBytes + (jsonBytes < 126 ? 2 : jsonBytes <= ushort.MaxValue ? 4 : 10);
	public static int SseBytes(long id, string json) => Encoding.UTF8.GetByteCount($"id: {id}\nevent: message\ndata: {json}\n\n");
	public static int LongPollingBytes(string jsonBody, int estimatedHeaders = 300) =>
		Encoding.UTF8.GetByteCount(jsonBody) + estimatedHeaders;
}
