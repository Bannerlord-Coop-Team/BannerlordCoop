#if DEBUG
using System;

namespace Common.Network;

/// <summary>DEBUG-only control for simulating a temporary blackholed network route.</summary>
public interface IDebugNetworkTrafficControl
{
    DateTime? TrafficPausedUntilUtc { get; }

    void PauseTraffic(TimeSpan duration);

    void ResumeTraffic();
}
#endif
