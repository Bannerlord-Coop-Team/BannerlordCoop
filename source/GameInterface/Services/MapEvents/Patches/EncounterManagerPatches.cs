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
public class EncounterManagerPatches
{
    private static AllowedInstance<MobileParty> _allowedInstance;


    //[HarmonyPatch("StartPartyEncounter")]
    //[HarmonyPrefix]
    //private static bool StartPartyEncounterPrefix() => false;

    [HarmonyPatch(typeof(PlayerEncounter), "EnterSettlement")]
    [HarmonyPrefix]
    private static bool EnterSettlementPrefix() 
    {
        if (_allowedInstance?.Instance == MobileParty.MainParty) return true;

        MessageBroker.Instance.Publish(MobileParty.MainParty, new SettlementEntered(MobileParty.MainParty.TargetSettlement.StringId, MobileParty.MainParty.StringId));

        return false;
    }
    public static void RunOriginalEnterSettlement()
    {

        using (_allowedInstance = new AllowedInstance<MobileParty>(MobileParty.MainParty))
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                PlayerEncounter.EnterSettlement();
            }, true);
        }
    }

    [HarmonyPatch(typeof(PlayerEncounter), "LeaveSettlement")]
    [HarmonyPrefix]
    public static bool LeaveSettlementPrefix()
    {
        if (_allowedInstance?.Instance == MobileParty.MainParty) return true;

        MessageBroker.Instance.Publish(MobileParty.MainParty, new SettlementLeft(MobileParty.MainParty.CurrentSettlement.StringId, MobileParty.MainParty.StringId));

        return false;
    }
    public static void RunOriginalLeaveSettlement()
    {

        using (_allowedInstance = new AllowedInstance<MobileParty>(MobileParty.MainParty))
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                PlayerEncounter.LeaveSettlement();
            }, true);
        }
    }

    [HarmonyPatch(typeof(LeaveSettlementAction), "ApplyForParty")]
    [HarmonyPrefix]
    public static bool LeaveSettlementActionPrefix(MobileParty mobileParty)
    {
        if (_allowedInstance?.Instance == mobileParty) return true;

        return false;
    }

    public static void RunOriginalLeaveSettlementAction(MobileParty mobileParty)
    {
        using (_allowedInstance = new AllowedInstance<MobileParty>(mobileParty))
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                LeaveSettlementAction.ApplyForParty(mobileParty);
            }, true);
        }
    }

}
