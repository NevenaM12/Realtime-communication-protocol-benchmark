namespace BenchmarkServer.Utilities;

public static class CgroupReader
{
	public static long? ReadMemory() => ReadLong("/sys/fs/cgroup/memory.current");
	public static long? ReadCpuUsageUsec()
	{
		try
		{
			if (!File.Exists("/sys/fs/cgroup/cpu.stat"))
				return null;
			var line = File.ReadLines("/sys/fs/cgroup/cpu.stat").FirstOrDefault(x => x.StartsWith("usage_usec "));
			return long.TryParse(line?.Split(' ')[1], out var v) ? v : null;
		}
		catch
		{
			return null;
		}
	}
	private static long? ReadLong(string p)
	{
		try
		{
			return File.Exists(p) && long.TryParse(File.ReadAllText(p), out var v) ? v : null;
		}
		catch
		{
			return null;
		}
	}
}
