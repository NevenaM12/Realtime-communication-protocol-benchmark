using BenchmarkServer.Routes;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://0.0.0.0:8080");

var app = builder.Build();
app.MapHealthRoutes();
app.Run();

public partial class Program
{
}
