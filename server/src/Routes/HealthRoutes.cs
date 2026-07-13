using BenchmarkServer.Utilities;
namespace BenchmarkServer.Routes;

public static class HealthRoutes
{
	public static void MapHealthRoutes(this WebApplication app)
	{
		app.MapGet("/health", () => Results.Ok(new { ok = true }));
		app.MapGet("/time", () => Results.Ok(new { server_time_ms = UnixTime.NowMilliseconds() }));
	}
}
