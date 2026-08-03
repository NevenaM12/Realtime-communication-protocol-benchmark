using LoadGenerator.Cli;

namespace LoadGenerator.Services;

public sealed class BenchmarkRunner(BenchmarkOptions options)
{
	private readonly HttpClient _http = new() { Timeout = Timeout.InfiniteTimeSpan };

	public async Task RunAsync()
	{
		var dir = Path.Combine(options.OutputDir, options.RunId);
		Directory.CreateDirectory(dir);
		await ResultWriter.WriteJsonAsync(Path.Combine(dir, "config.json"), options);

		using var setupCts = new CancellationTokenSource(TimeSpan.FromSeconds(options.SetupTimeoutSeconds));
		await WaitForHealthAsync(setupCts.Token);
		Console.WriteLine($"Load generator foundation is ready for run {options.RunId}.");
	}

	private async Task WaitForHealthAsync(CancellationToken token)
	{
		Exception? last = null;
		while (!token.IsCancellationRequested)
		{
			try
			{
				using var response = await _http.GetAsync(options.ServerUrl.TrimEnd('/') + "/health", token);
				if (response.IsSuccessStatusCode)
					return;
				last = new HttpRequestException($"Health endpoint returned {(int)response.StatusCode}.");
			}
			catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
			{
				last = ex;
			}

			await Task.Delay(250, token);
		}

		throw new InvalidOperationException("Benchmark server did not become healthy.", last);
	}
}
