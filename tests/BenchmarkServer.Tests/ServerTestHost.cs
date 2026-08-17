using BenchmarkServer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace BenchmarkServer.Tests;

internal sealed class ServerTestHost : IAsyncDisposable
{
	private readonly WebApplication _app;
	private readonly string? _previousResultsDirectory;

	private ServerTestHost(
		WebApplication app,
		Uri baseAddress,
		string resultsDirectory,
		string? previousResultsDirectory)
	{
		_app = app;
		BaseAddress = baseAddress;
		ResultsDirectory = resultsDirectory;
		_previousResultsDirectory = previousResultsDirectory;
	}

	public Uri BaseAddress { get; }
	public string ResultsDirectory { get; }

	public static async Task<ServerTestHost> StartAsync()
	{
		var resultsDirectory = Path.Combine(
			Path.GetTempPath(),
			"benchmark-server-tests",
			Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(resultsDirectory);

		var previousResultsDirectory = Environment.GetEnvironmentVariable("RESULTS_DIR");
		Environment.SetEnvironmentVariable("RESULTS_DIR", resultsDirectory);
		var app = BenchmarkServerApplication.Build([], "http://127.0.0.1:0");

		try
		{
			await app.StartAsync();
			var addresses = app.Services
				.GetRequiredService<IServer>()
				.Features
				.Get<IServerAddressesFeature>()?
				.Addresses;
			var address = addresses?.SingleOrDefault(value => value.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
				?? throw new InvalidOperationException("The test server did not expose an HTTP address.");

			return new ServerTestHost(app, new Uri(address), resultsDirectory, previousResultsDirectory);
		}
		catch
		{
			await app.DisposeAsync();
			Environment.SetEnvironmentVariable("RESULTS_DIR", previousResultsDirectory);
			Directory.Delete(resultsDirectory, true);
			throw;
		}
	}

	public HttpClient CreateHttpClient() => new()
	{
		BaseAddress = BaseAddress,
		Timeout = Timeout.InfiniteTimeSpan
	};

	public async ValueTask DisposeAsync()
	{
		try
		{
			await _app.StopAsync();
			await _app.DisposeAsync();
		}
		finally
		{
			Environment.SetEnvironmentVariable("RESULTS_DIR", _previousResultsDirectory);
			if (Directory.Exists(ResultsDirectory))
				Directory.Delete(ResultsDirectory, true);
		}
	}
}
