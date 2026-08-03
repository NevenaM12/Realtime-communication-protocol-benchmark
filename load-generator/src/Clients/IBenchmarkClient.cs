using LoadGenerator.Models;
namespace LoadGenerator.Clients;

public interface IBenchmarkClient : IAsyncDisposable
{
	int Id
	{
		get;
	}
	double SetupTimeMs
	{
		get;
	}
	long PollRequests
	{
		get;
	}
	long EmptyPollResponses
	{
		get;
	}
	long ResponseBodyBytes
	{
		get;
	}
	Task ConnectAsync(CancellationToken token);
	Task RunAsync(Func<int, BenchmarkMessage, int, long, Task> onMessage, CancellationToken token);
}
