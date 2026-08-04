namespace LoadGenerator.Metrics;

public sealed class LossTracker
{
	private readonly object _gate = new();
	private readonly SortedList<long, long> _missingRanges = [];
	private long _accountedThrough;
	private long _first;
	private long _last;
	private long _missing;
	private long _duplicates;
	private long _outOfOrder;
	private long _count;

	public long First
	{
		get
		{
			lock (_gate)
				return _first;
		}
	}

	public long Last
	{
		get
		{
			lock (_gate)
				return _last;
		}
	}

	public long Missing
	{
		get
		{
			lock (_gate)
				return _missing;
		}
	}

	public long Duplicates
	{
		get
		{
			lock (_gate)
				return _duplicates;
		}
	}

	public long OutOfOrder
	{
		get
		{
			lock (_gate)
				return _outOfOrder;
		}
	}

	public long Count
	{
		get
		{
			lock (_gate)
				return _count;
		}
	}

	public long UniqueCount
	{
		get
		{
			lock (_gate)
				return _count - _duplicates;
		}
	}

	public void Record(long id)
	{
		if (id <= 0)
			throw new ArgumentOutOfRangeException(nameof(id), "Message IDs must be positive.");

		lock (_gate)
		{
			_count++;

			if (id > _accountedThrough)
			{
				AddMissingRange(_accountedThrough + 1, id - 1);
				_accountedThrough = id;
				if (_first == 0)
					_first = id;
				_last = id;
				return;
			}

			if (RemoveMissing(id))
			{
				if (_first == 0)
					_first = id;
				if (id < _last)
					_outOfOrder++;
				_last = Math.Max(_last, id);
				return;
			}

			_duplicates++;
		}
	}

	public void Complete(long finalMessageId)
	{
		if (finalMessageId < 0)
			throw new ArgumentOutOfRangeException(nameof(finalMessageId));

		lock (_gate)
		{
			if (finalMessageId <= _accountedThrough)
				return;

			AddMissingRange(_accountedThrough + 1, finalMessageId);
			_accountedThrough = finalMessageId;
		}
	}

	private void AddMissingRange(long start, long end)
	{
		if (start > end)
			return;

		_missingRanges.Add(start, end);
		_missing += end - start + 1;
	}

	private bool RemoveMissing(long id)
	{
		var low = 0;
		var high = _missingRanges.Count - 1;
		var candidate = -1;

		while (low <= high)
		{
			var middle = low + (high - low) / 2;
			if (_missingRanges.Keys[middle] <= id)
			{
				candidate = middle;
				low = middle + 1;
			}
			else
			{
				high = middle - 1;
			}
		}

		if (candidate < 0)
			return false;

		var start = _missingRanges.Keys[candidate];
		var end = _missingRanges.Values[candidate];
		if (id > end)
			return false;

		if (start == end)
		{
			_missingRanges.RemoveAt(candidate);
		}
		else if (id == start)
		{
			_missingRanges.RemoveAt(candidate);
			_missingRanges.Add(start + 1, end);
		}
		else if (id == end)
		{
			_missingRanges[start] = end - 1;
		}
		else
		{
			_missingRanges[start] = id - 1;
			_missingRanges.Add(id + 1, end);
		}

		_missing--;
		return true;
	}
}
