using BenchmarkServer.Protocols;
using BenchmarkServer.Routes;
using BenchmarkServer.Services;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://0.0.0.0:8080");
builder.Services.AddSingleton<BenchmarkState>();
builder.Services.AddSingleton<MessageGenerator>();

var app = builder.Build();
app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(30) });
app.MapHealthRoutes();
app.MapControlRoutes();
app.MapStatsRoutes();
app.MapWebSocketProtocol();
app.MapSseProtocol();
app.MapLongPollingProtocol();
app.Run();

public partial class Program
{
}
