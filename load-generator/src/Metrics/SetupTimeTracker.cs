namespace LoadGenerator.Metrics;

public static class SetupTimeTracker
{
	public static double Average(IEnumerable<double> x) => x.Any() ? x.Average() : 0;

	public static double P95(IEnumerable<double> x)
	{
		var a = x.Order().ToArray();
		return a.Length == 0
			? 0
			: a[(int)Math.Ceiling(a.Length * .95) - 1];
	}
}
