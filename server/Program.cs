using BenchmarkServer.Routes;
using BenchmarkServer.Services;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://0.0.0.0:8080");
builder.Services.AddSingleton<BenchmarkState>();
builder.Services.AddSingleton<MessageGenerator>();

var app = builder.Build();
app.MapHealthRoutes();
app.MapControlRoutes();
app.MapStatsRoutes();
app.Run();

public partial class Program
{
}
