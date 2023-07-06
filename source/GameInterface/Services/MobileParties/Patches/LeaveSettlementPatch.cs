using HarmonyLib;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// Patches leaving settlement to remove any leaves from null settlement
/// </summary>

[HarmonyPatch(typeof(LeaveSettlementAction))]
public class LeaveSettlementPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(LeaveSettlementAction.ApplyForParty))]
    static bool Prefix(MobileParty mobileParty)
    {
        //if(mobileParty.StringId != "TransferredParty") { return true; }
        if (mobileParty.CurrentSettlement == null) { return false; }
        return true;
    }
}
