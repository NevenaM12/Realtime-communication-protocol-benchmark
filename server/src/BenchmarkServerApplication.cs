using BenchmarkServer.Protocols;
using BenchmarkServer.Routes;
using BenchmarkServer.Services;

namespace BenchmarkServer;

public static class BenchmarkServerApplication
{
	public static WebApplication Build(string[] args, string? urls = null)
	{
		var builder = WebApplication.CreateBuilder(args);
		builder.WebHost.UseUrls(urls ?? "http://0.0.0.0:8080");
		builder.Services.AddSingleton<BenchmarkState>();
		builder.Services.AddSingleton<MessageGenerator>();
		builder.Services.AddSingleton<ResourceSampler>();

		var app = builder.Build();
		app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(30) });
		app.MapHealthRoutes();
		app.MapControlRoutes();
		app.MapStatsRoutes();
		app.MapWebSocketProtocol();
		app.MapSseProtocol();
		app.MapLongPollingProtocol();
		return app;
	}
}
