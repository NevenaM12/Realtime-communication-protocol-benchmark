using BenchmarkServer.Models;

namespace BenchmarkServer.Services;

public sealed class MessageBuffer
{
	private readonly object _gate = new();
	private readonly Queue<BenchmarkMessage> _items = new();
	private int _capacity = 4096;

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
			if (_items.Count >= _capacity)
        		_items.Dequeue();
    		_items.Enqueue(message);
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
		var truncated = _items.TryPeek(out var oldest) && lastId < oldest.Id - 1;
		var limit = Math.Clamp(maxBatch, 1, _capacity);
		var messages = new List<BenchmarkMessage>(Math.Min(_items.Count, limit));
		foreach (var message in _items)
		{
			if (message.Id <= lastId)
				continue;
			messages.Add(message);
			if (messages.Count == limit)
				break;
		}

		return (messages, truncated);
	}
}
