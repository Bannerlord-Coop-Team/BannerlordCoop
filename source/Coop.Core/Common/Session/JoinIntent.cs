namespace Coop.Core.Common.Session;

/// <summary>
/// Why a client session is being started. Every join path funnels through the same client start,
/// so the caller's intent is the only thing that tells them apart.
/// </summary>
public enum JoinIntent
{
    /// <summary>The player entered an address on the co-op join screen.</summary>
    PlayerDirect,

    /// <summary>The player picked a Steam lobby, or accepted an invite.</summary>
    PlayerSteam,

    /// <summary>This instance hosted, and is connecting to its own standalone server process.</summary>
    HostLoopback,
}
