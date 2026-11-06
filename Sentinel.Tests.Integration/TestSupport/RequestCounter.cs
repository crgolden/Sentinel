namespace Sentinel.Tests.Integration.TestSupport;

public sealed class RequestCounter
{
    private int _count;

    public int Count => Volatile.Read(ref _count);

    public void Increment() => Interlocked.Increment(ref _count);
}
