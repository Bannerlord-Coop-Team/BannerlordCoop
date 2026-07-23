using Common.Network.Session;

namespace Coop.Core.Common.Session;

/// <summary>
/// Process-wide facts a server was launched with, parsed once at mod load. Empty in every
/// other process (clients, and manually launched servers that pass neither argument).
/// </summary>
public static class ManagedServerConfig
{
    /// <summary>A save was named on the command line to auto-load, managed or not.</summary>
    public static bool HasAutoLoadSave => SaveName != null;

    /// <summary>
    /// This server was spawned by the in-game Host flow. The spawning client and player
    /// population do not own its lifetime; it remains open until the user closes its process.
    /// A manually launched server that only passes /coopsave is not marked as managed.
    /// </summary>
    public static bool IsManagedServer => OwnerProcessId > 0;

    /// <summary>The save the server auto-loads.</summary>
    public static string SaveName { get; set; }

    /// <summary>Process id of the hosting client that spawned this server; 0 when manually launched.</summary>
    public static int OwnerProcessId { get; set; }

    /// <summary>Optional connection password supplied on the server command line.</summary>
    public static string Password { get; set; } = string.Empty;

    /// <summary>Who can discover this server through Steam.</summary>
    public static ServerVisibility Visibility { get; set; } = ServerVisibility.Public;
}
