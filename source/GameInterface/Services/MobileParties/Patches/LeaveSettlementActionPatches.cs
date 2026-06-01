using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.MobileParties.Extensions;
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

        var message = new PartyLeaveSettlementAttempted(mobileParty);
        MessageBroker.Instance.Publish(mobileParty, message);

        return false;
    }

    public static void OverrideApplyForParty(MobileParty party)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                if (party.CurrentSettlement != null)
                    LeaveSettlementAction.ApplyForParty(party);

                //Stop a player party from immediately re-engaging the settlement it just
                //left. Without this the server's EncounterManager tick can restart the
                //settlement encounter on the next frame (before the client's "hold" order
                //syncs back)
                if (party.IsPlayerParty())
                    party.SetMoveModeHold();
            }
        });
    }
}
