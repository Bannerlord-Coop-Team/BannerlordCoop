using GameInterface.Services.MobileParties.Extensions;
using System;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Utils;

/// <summary>
/// Filters inbound AutoSync applies that would overwrite state the LOCAL instance is
/// authoritative for. The movement members stream from the server as dead reckoning for
/// parties this instance does NOT control; for the locally-controlled party (the player's
/// own) the local simulation plus the explicit behavior pipeline (UpdatePartyBehavior) are
/// authoritative. Applying the server's copy on top fights the local simulation — the
/// server's stale MoveTargetPoint dragged a mid-bridge player party off the crossing onto
/// open water, and spam-clicking orders made it reliably reproducible: each click resets
/// both simulations out of phase, and one applied server step-target that sits past the
/// bridge makes the local party straight-step off it.
/// </summary>
public static class AutoSyncOwnershipGate
{
    // Member names as they appear inside the generated message type names
    // (e.g. MobileParty_MoveTargetPoint_SetNetworkMessage). Underscore-delimited so
    // one member name can never prefix-match another.
    private static readonly string[] OwnerAuthoritativeMovementMembers =
    {
        "_MoveTargetPoint_",
        "__targetSettlement_",
        "_TargetParty_",
        "_DefaultBehavior_",
        "_ShortTermBehavior_",
        "_DesiredAiNavigationType_",
    };

    public static bool ShouldSkipInboundApply(object instance, Type messageType)
    {
        if (instance is not MobileParty party) return false;
        if (!party.IsControlledByThisInstance()) return false;

        string name = messageType.Name;
        foreach (string member in OwnerAuthoritativeMovementMembers)
        {
            if (name.Contains(member)) return true;
        }
        return false;
    }
}
