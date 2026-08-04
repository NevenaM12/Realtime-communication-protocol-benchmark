namespace LoadGenerator.Metrics;

public sealed class ByteAccounting
{
	public long PayloadBytes;
	public long EncodedMessageBytes;
	public long EstimatedProtocolBytes;

	public void Add(int payload, int encoded, int protocol)
	{
		Interlocked.Add(ref PayloadBytes, payload);
		Interlocked.Add(ref EncodedMessageBytes, encoded);
		Interlocked.Add(ref EstimatedProtocolBytes, protocol);
	}

	public long EstimatedOverheadBytes => Math.Max(0, EstimatedProtocolBytes - PayloadBytes);
	public double OverheadRatio => PayloadBytes == 0 ? 0 : EstimatedOverheadBytes / (double)PayloadBytes;
}
