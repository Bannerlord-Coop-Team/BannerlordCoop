using Common.Messaging;
using Common.Util;
using GameInterface.Services.MobileParties.Extensions;
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
    public static readonly AllowedInstance<MobileParty> AllowedInstance = new AllowedInstance<MobileParty>();

    [HarmonyPrefix]
    [HarmonyPatch(nameof(LeaveSettlementAction.ApplyForParty))]
    private static bool Prefix(MobileParty mobileParty)
    {
        if(AllowedInstance.IsAllowed(mobileParty)) return true;

        var message = new PartyLeaveSettlementAttempted(mobileParty.StringId);
        MessageBroker.Instance.Publish(mobileParty, message);

        return false;
    }

    private static object _lock = new object();
    public static void OverrideApplyForParty(MobileParty party)
    {
        using(AllowedInstance)
        {
            AllowedInstance.Instance = party;

            if (party.CurrentSettlement is null) return;
            LeaveSettlementAction.ApplyForParty(party);
        }
    }
}
