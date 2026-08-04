namespace LoadGenerator.Metrics;

public static class ThroughputTracker
{
	public static double Calculate(long count, double seconds) => seconds <= 0 ? 0 : count / seconds;
}
