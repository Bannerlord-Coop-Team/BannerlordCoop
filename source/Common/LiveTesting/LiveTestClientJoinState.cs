#if DEBUG
using System.Threading;

namespace Common.LiveTesting;

/// <summary>Tracks deferred join attempts until the client session has fully torn down.</summary>
public interface ILiveTestClientJoinState
{
    bool Attempted { get; }
    bool Refresh(bool atMainMenu, bool campaignLoaded, bool sessionActive);
    bool TryBegin();
    void CompleteStart(bool started);
}

/// <inheritdoc />
public sealed class LiveTestClientJoinState : ILiveTestClientJoinState
{
    private int attempted;

    public bool Attempted => Volatile.Read(ref attempted) != 0;

    public bool Refresh(bool atMainMenu, bool campaignLoaded, bool sessionActive)
    {
        if (!atMainMenu || campaignLoaded || sessionActive) return false;
        Interlocked.Exchange(ref attempted, 0);
        return true;
    }

    public bool TryBegin() => Interlocked.CompareExchange(ref attempted, 1, 0) == 0;

    public void CompleteStart(bool started) => Interlocked.Exchange(ref attempted, started ? 1 : 0);
}
#endif
