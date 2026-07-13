using BenchmarkServer.Services;
namespace BenchmarkServer.Routes;

public static class StatsRoutes
{
	public static void MapStatsRoutes(this WebApplication app) => app.MapGet("/stats", (BenchmarkState s) => Results.Ok(s.Snapshot()));
}
