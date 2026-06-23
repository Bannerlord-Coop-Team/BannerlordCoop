using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MobileParties.Messages.Behavior;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// Patches leaving settlement to remove any leaves from null settlement
/// </summary>

[HarmonyPatch(typeof(LeaveSettlementAction))]
public class LeaveSettlementActionPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(LeaveSettlementAction.ApplyForParty))]
    private static bool Prefix(MobileParty mobileParty)
    {
        if (mobileParty.CurrentSettlement == null) return false;
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        var message = new PartyLeaveSettlementAttempted(mobileParty);
        MessageBroker.Instance.Publish(mobileParty, message);

        // Client blocks (the server-applied leave replicates back); server runs the original so the
        // leave actually happens. The server intentionally applies here rather than re-applying from
        // the handler, which (without an allowed thread) would re-enter this prefix and recurse.
        return ModInformation.IsServer;
    }
}
