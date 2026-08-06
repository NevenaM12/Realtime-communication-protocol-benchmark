using BenchmarkServer.Models;

namespace BenchmarkServer.Services;

public sealed class MessageBuffer
{
	private readonly object _gate = new();
	private readonly List<BenchmarkMessage> _items = [];
	private int _capacity = 10000;
	private TaskCompletionSource _changed = NewSignal();

	private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

	public void Reset(int capacity)
	{
		lock (_gate)
		{
			_items.Clear();
			_capacity = capacity;
			_changed.TrySetResult();
			_changed = NewSignal();
		}
	}

	public void Append(BenchmarkMessage message)
	{
		lock (_gate)
		{
			_items.Add(message);
			if (_items.Count > _capacity)
				_items.RemoveRange(0, _items.Count - _capacity);
			_changed.TrySetResult();
			_changed = NewSignal();
		}
	}

	public (IReadOnlyList<BenchmarkMessage> Messages, bool Truncated) ReadAfter(long lastId, int maxBatch)
	{
		lock (_gate)
			return ReadAfterLocked(lastId, maxBatch);
	}

	public (IReadOnlyList<BenchmarkMessage> Messages, bool Truncated, Task ChangeTask) ReadAfterAndWatch(long lastId, int maxBatch)
	{
		lock (_gate)
		{
			var result = ReadAfterLocked(lastId, maxBatch);
			return (result.Messages, result.Truncated, _changed.Task);
		}
	}

	private (IReadOnlyList<BenchmarkMessage> Messages, bool Truncated) ReadAfterLocked(long lastId, int maxBatch)
	{
		var truncated = _items.Count > 0 && lastId < _items[0].Id - 1;
		return (_items.Where(x => x.Id > lastId).Take(Math.Clamp(maxBatch, 1, 10000)).ToArray(), truncated);
	}
}
