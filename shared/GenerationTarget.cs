namespace BenchmarkShared;

public static class GenerationTarget
{
	public static long Messages(int ratePerSecond, int durationSeconds, long? totalMessages)
	{
		var durationTarget = durationSeconds > 0
			? (long)ratePerSecond * durationSeconds
			: 0;
		var countTarget = totalMessages > 0 ? totalMessages.Value : 0;

		if (durationTarget > 0 && countTarget > 0)
			return Math.Min(durationTarget, countTarget);

		return Math.Max(durationTarget, countTarget);
	}

	public static double AchievementRatio(long generatedMessages, long targetMessages) =>
		targetMessages <= 0 ? 0 : generatedMessages / (double)targetMessages;
}
