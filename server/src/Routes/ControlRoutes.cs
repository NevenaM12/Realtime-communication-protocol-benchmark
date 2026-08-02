using System.Text.Json;
using BenchmarkServer.Models;
using BenchmarkServer.Services;

namespace BenchmarkServer.Routes;

public static class ControlRoutes
{
	public static void MapControlRoutes(this WebApplication app)
	{
		app.MapPost(
			"/control/start",
			async (BenchmarkRunConfig config, BenchmarkState state, MessageGenerator generator, ResourceSampler sampler) =>
			{
				try
				{
					var token = state.Start(config);
					var dir = Path.Combine(
						Environment.GetEnvironmentVariable("RESULTS_DIR") ?? "/app/results",
						config.RunId);

					Directory.CreateDirectory(dir);
					await File.WriteAllTextAsync(
						Path.Combine(dir, "server_config.json"),
						JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
					sampler.Start(config.RunId, token);
					await generator.StartAsync(config, token);

					return Results.Ok(new
					{
						started = true,
						runId = config.RunId,
						config
					});
				}
				catch (Exception e) when (e is ArgumentException or InvalidOperationException)
				{
					return Results.Conflict(new { error = e.Message });
				}
			});

		app.MapPost(
			"/control/stop",
			async (BenchmarkState state, MessageGenerator generator, ResourceSampler sampler) =>
			{
				var runId = state.Config?.RunId;
				state.Stop();
				await generator.WaitAsync();
				await sampler.StopAsync();
				var stats = state.Snapshot(runId);

				if (runId is not null)
				{
					var path = Path.Combine(
						Environment.GetEnvironmentVariable("RESULTS_DIR") ?? "/app/results",
						runId,
						"server_final_stats.json");
					await File.WriteAllTextAsync(
						path,
						JsonSerializer.Serialize(stats, new JsonSerializerOptions { WriteIndented = true }));
				}

				return Results.Ok(stats);
			});
	}
}
