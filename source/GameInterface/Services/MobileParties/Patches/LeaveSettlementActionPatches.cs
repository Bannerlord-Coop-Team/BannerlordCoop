using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.MobileParties.Messages.Behavior;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

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

        if (ModInformation.IsClient) return false;

        var message = new PartyLeaveSettlementAttempted(mobileParty.StringId);
        MessageBroker.Instance.Publish(mobileParty, message);

        return false;
    }

    public static void OverrideApplyForParty(MobileParty party)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                if (party.CurrentSettlement != null) LeaveSettlementAction.ApplyForParty(party);
            }
        }, blocking: true);
    }
}
