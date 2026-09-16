namespace ShopLite.Api.Chaos;

public record ChaosConfig(int LatencyMs = 0, double ErrorRate = 0, bool SlowQuery = false);

/// <summary>
/// Thread-safe holder for chaos settings. The config is immutable and swapped atomically, 
/// so concurrent requests always see a consistent snapshot without locks.
/// </summary>
public class ChaosState
{
    private ChaosConfig _current = new();

    public ChaosConfig Current => Volatile.Read(ref _current);
    public void Update(ChaosConfig config) => Volatile.Write(ref _current, config);
}