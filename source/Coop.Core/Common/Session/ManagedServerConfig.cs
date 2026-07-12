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
    /// This server is spawned and owned by a hosting client, so it manages its own
    /// lifetime (quits when empty or abandoned). A manually launched server that only
    /// passes /coopsave auto-loads but is NOT managed, so it stays up indefinitely.
    /// </summary>
    public static bool IsManagedServer => OwnerProcessId > 0;

    /// <summary>The save the server auto-loads, and a managed server saves back on shutdown.</summary>
    public static string SaveName { get; set; }

    /// <summary>Process id of the hosting client that spawned this server; 0 when unmanaged.</summary>
    public static int OwnerProcessId { get; set; }

    /// <summary>Optional connection password supplied on the server command line.</summary>
    public static string Password { get; set; } = string.Empty;
}
