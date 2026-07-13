namespace BenchmarkServer.Utilities;

public static class UnixTime
{
	public static long NowMilliseconds() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
