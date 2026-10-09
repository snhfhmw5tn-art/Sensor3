namespace Sensor3;

public sealed class NativeObservationLifetime
{
    private readonly object gate = new();
    private CancellationTokenSource current = new();
    public CancellationTokenSource Create(CancellationToken token)
    {
        lock (gate) return CancellationTokenSource.CreateLinkedTokenSource(token, current.Token);
    }
    public void CancelActive()
    {
        lock (gate) { current.Cancel(); current.Dispose(); current = new(); }
    }
}
