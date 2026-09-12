using System.Collections.Generic;

namespace Coop.Steam;

/// <summary>
/// Tracks the authenticated remote identity for each owned Steam connection. Synchronization
/// is supplied by the transport so ownership and identity changes stay atomic.
/// </summary>
internal sealed class TunnelConnectionIdentityRegistry
{
    private readonly Dictionary<uint, ulong> identities = new Dictionary<uint, ulong>();

    public void Record(uint connection, ulong steamId)
    {
        if (steamId == 0)
        {
            identities.Remove(connection);
            return;
        }

        identities[connection] = steamId;
    }

    public bool TryGet(uint connection, out ulong steamId) =>
        identities.TryGetValue(connection, out steamId);

    public void Remove(uint connection) => identities.Remove(connection);

    public void Clear() => identities.Clear();
}
