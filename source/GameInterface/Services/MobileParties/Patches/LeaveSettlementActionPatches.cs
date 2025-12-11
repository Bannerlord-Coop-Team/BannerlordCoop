using Common;
using Common.Messaging;
using Common.Logging;
using Serilog;
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
    private static ILogger Logger = LogManager.GetLogger<LeaveSettlementActionPatches>();
    [HarmonyPrefix]
    [HarmonyPatch(nameof(LeaveSettlementAction.ApplyForParty))]
    private static bool Prefix(MobileParty mobileParty)
    {
        Logger.Information(
            "LeaveSettlementAction.ApplyForParty intercept party={partyId} current={current}",
            mobileParty.StringId,
            mobileParty.CurrentSettlement?.StringId ?? "none");
        if (mobileParty.CurrentSettlement == null) return false;
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient) return false;

        var message = new PartyLeaveSettlementAttempted(mobileParty.StringId);
        MessageBroker.Instance.Publish(mobileParty, message);

        return false;
    }

    public static void OverrideApplyForParty(MobileParty party)
    {
        Logger.Information(
            "Override LeaveSettlementAction.ApplyForParty party={partyId} current={current}",
            party.StringId,
            party.CurrentSettlement?.StringId ?? "none");
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                if (party.CurrentSettlement != null) LeaveSettlementAction.ApplyForParty(party);
            }
        }, blocking: true);
        Logger.Information(
            "Override LeaveSettlementAction.ApplyForParty terminé party={partyId} current={current}",
            party.StringId,
            party.CurrentSettlement?.StringId ?? "none");
    }
}
