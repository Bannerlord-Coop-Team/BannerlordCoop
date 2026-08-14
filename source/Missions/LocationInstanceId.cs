using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;

namespace Missions;

/// <summary>
/// Derives the id of a P2P mission instance for a settlement location. Uses <b>ObjectManager ids</b>
/// (NOT StringId) joined with '|', so co-located clients independently arrive at the same id and the
/// NAT-punch and relay-membership paths agree on one instance. This is the single source of the format —
/// every site that needs a location instance id should call it.
/// </summary>
public static class LocationInstanceId
{
    public static bool TryDerive(IObjectManager objectManager, Settlement settlement, Location location, out string instanceId)
    {
        instanceId = null;

        if (settlement == null || location == null) return false;
        if (objectManager.TryGetIdWithLogging(settlement, out var settlementId) == false) return false;
        if (objectManager.TryGetIdWithLogging(location, out var locationId) == false) return false;

        instanceId = $"{settlementId}|{locationId}";
        return true;
    }

    /// <summary>
    /// Extract the settlement's ObjectManager id back out of an instance id. Used by the server-side
    /// host election to validate that a requester's party is actually in the instance's settlement —
    /// the server never opens location missions, so the id string is all it has.
    /// </summary>
    public static bool TryGetSettlementId(string instanceId, out string settlementId)
    {
        settlementId = null;
        if (string.IsNullOrEmpty(instanceId)) return false;

        var separatorIndex = instanceId.IndexOf('|');
        if (separatorIndex <= 0) return false;

        settlementId = instanceId.Substring(0, separatorIndex);
        return true;
    }
}
