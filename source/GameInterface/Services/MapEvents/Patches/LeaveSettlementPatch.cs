using Common.Util;
using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Common.Messaging;
using GameInterface.Services.MobileParties.Messages.Control;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Disables all map encounters/events
/// </summary>

[HarmonyPatch(typeof(LeaveSettlementAction), nameof(LeaveSettlementAction.ApplyForParty))]
public class LeaveSettlementPatch
{
    static bool Prefix(MobileParty mobileParty)
    {
        if(mobileParty.StringId != "TransferredParty") { return true; }
        if(mobileParty.CurrentSettlement == null) { return false; }
        return true;
    }
}
