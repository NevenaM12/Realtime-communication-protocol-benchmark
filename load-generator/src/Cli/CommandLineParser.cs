namespace LoadGenerator.Cli;

public static class CommandLineParser
{
	public static BenchmarkOptions Parse(string[] args)
	{
		var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		for (var i = 0; i < args.Length; i++)
		{
			if (!args[i].StartsWith("--"))
				throw new ArgumentException($"Unexpected argument: {args[i]}");
			var k = args[i][2..];
			var v = i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[++i] : "true";
			d[k] = v;
		}

		string Get(string k, string? x = null) => d.TryGetValue(k, out var v) ? v : x ?? throw new ArgumentException($"Missing --{k}");
		int Int(string k, int x) => int.Parse(Get(k, x.ToString()));
		var protocol = Get("protocol").ToLowerInvariant();

		if (protocol is not ("ws" or "sse" or "lp"))
			throw new ArgumentException("--protocol must be ws, sse, or lp");

		var clients = Int("clients", 1);
		var size = Int("payload-size", 1024);
		var rate = Int("rate", 10);
		long? totalMessages = d.TryGetValue("total-messages", out var tm) ? long.Parse(tm) : null;
		var duration = Int("duration", totalMessages is null ? 60 : 0);
		if (clients <= 0 || size < 0 || rate <= 0)
			throw new ArgumentException("clients and rate must be positive and payload size non-negative");
		if (duration < 0 || (duration == 0 && totalMessages is null))
			throw new ArgumentException("--duration must be non-negative and can be zero only when --total-messages is specified");
		if (totalMessages <= 0)
			throw new ArgumentException("--total-messages must be positive");

		return new(
			protocol,
			clients,
			size,
			rate,
			duration,
			totalMessages,
			Get("run-id", $"{protocol}_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}"),
			Get("server-url", Environment.GetEnvironmentVariable("SERVER_URL") ?? "http://benchmark-server:8080"),
			Int("warmup-seconds", 5),
			Int("cooldown-seconds", 2),
			Int("long-poll-timeout-ms", 30000),
			Int("long-poll-max-batch", 100),
			Get("output-dir", "/app/results"),
			bool.Parse(Get("raw-log", "false")),
			Int("raw-log-limit", 100000),
			Int("setup-timeout-seconds", 60),
			Int("clock-sync-samples", 10));
	}
}
