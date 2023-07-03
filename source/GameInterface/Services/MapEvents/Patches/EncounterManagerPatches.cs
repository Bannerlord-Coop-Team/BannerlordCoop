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
    private static AllowedInstance<Settlement> _allowedInstance;
    private static MobileParty _party;
    private static Settlement _settlement;


    //[HarmonyPatch("StartPartyEncounter")]
    //[HarmonyPrefix]
    //private static bool StartPartyEncounterPrefix() => false;

    [HarmonyPatch(typeof(PlayerEncounter), "EnterSettlement")]
    [HarmonyPrefix]
    private static bool EnterSettlementPrefix() 
    {
        if (_allowedInstance?.Instance != null) return true;

        MessageBroker.Instance.Publish(MobileParty.MainParty, new SettlementEntered(MobileParty.MainParty.TargetSettlement.StringId, MobileParty.MainParty.Id.ToString()));

        return false;
    }

    public static void RunOriginalEnterSettlement(MobileParty attackerParty, Settlement settlement)
    {

        using (_allowedInstance = new AllowedInstance<Settlement>(settlement))
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                PlayerEncounter.EnterSettlement();
            }, true);
        }
    }
}
