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

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Disables all map encounters/events
/// </summary>
[HarmonyPatch(typeof(EnterSettlementAction))]
public class EncounterManagerPatches
{
    private static AllowedInstance<MobileParty> _allowedInstance;

    //[HarmonyPatch("StartPartyEncounter")]
    //[HarmonyPrefix]
    //private static bool StartPartyEncounterPrefix() => false;

    [HarmonyPatch("ApplyForParty")]
    [HarmonyPrefix]
    private static bool ApplyForPartyPrefix(MobileParty mobileParty, Settlement settlement) 
    {
        if (_allowedInstance?.Instance == mobileParty) return true;

        if (mobileParty != MobileParty.MainParty) return false;

        MessageBroker.Instance.Publish(mobileParty, new SettlementEntered(settlement.StringId, mobileParty.Id.ToString()));

        return false;
    }

    public static void RunOriginalStartSettlementEncounter(MobileParty mobileParty, Settlement settlement)
    {
        using (_allowedInstance = new AllowedInstance<MobileParty>(mobileParty))
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                EnterSettlementAction.ApplyForParty(mobileParty, settlement);
            }, true);
        }
    }
}

public class PlayerEncounterPatches
{
    //[HarmonyPatch("LeaveSettlement")]
    //[HarmonyPrefix]
    //private static bool LeaveSettlementPrefix()
}
