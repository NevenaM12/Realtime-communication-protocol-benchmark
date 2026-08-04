namespace LoadGenerator.Metrics;

public sealed class LatencyHistogram
{
	private const int Resolution = 10;
	private const int MaxBucket = 60000;
	private readonly long[] _buckets = new long[MaxBucket * Resolution + 2];
	private readonly object _gate = new();
	private long _count;
	private double _sum;
	private double _min = double.MaxValue;
	private double _max = double.MinValue;

	public long Count => Interlocked.Read(ref _count);

	public void Record(double ms)
	{
		lock (_gate)
		{
			_count++;
			_sum += ms;
			_min = Math.Min(_min, ms);
			_max = Math.Max(_max, ms);
			var i = (int)Math.Clamp(Math.Round(Math.Max(0, ms) * Resolution), 0, _buckets.Length - 1);
			_buckets[i]++;
		}
	}

	public double Average
	{
		get
		{
			lock (_gate)
				return _count == 0 ? 0 : _sum / _count;
		}
	}

	public double Min
	{
		get
		{
			lock (_gate)
				return _count == 0 ? 0 : _min;
		}
	}

	public double Max
	{
		get
		{
			lock (_gate)
				return _count == 0 ? 0 : _max;
		}
	}

	public double Percentile(double p)
	{
		lock (_gate)
		{
			if (_count == 0)
				return 0;
			var target = (long)Math.Ceiling(_count * p);
			long n = 0;
			for (var i = 0; i < _buckets.Length; i++)
			{
				if ((n += _buckets[i]) >= target)
					return i / (double)Resolution;
			}

			return _max;
		}
	}
}
