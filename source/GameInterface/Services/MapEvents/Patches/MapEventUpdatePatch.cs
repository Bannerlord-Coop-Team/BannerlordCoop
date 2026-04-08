using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace GameInterface.Services.MapEvents.Patches
{
    [HarmonyPatch(typeof(MapEvent))]
    public class MapEventUpdatePatch
    {
        [HarmonyPrefix]
        [HarmonyPatch("Update")]
        static bool PrefixUpdate(MapEvent __instance)
        {
            if (ModInformation.IsClient) return false;

            // Don't update if a player is involved
            // Prevents server from instantly finishing the battle and waits for client finish request
            if (__instance.InvolvedParties.Any(x => x.MobileParty.IsPartyControlled() == false)) return false;

            return true;
        }
    }

    [HarmonyPatch(typeof(MapEvent))]
    public class MapEventInitPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(MapEvent.Initialize))]
        static void Postfix(MapEvent __instance, PartyBase attackerParty, PartyBase defenderParty, MapEventComponent component, MapEvent.BattleTypes mapEventType)
        {
            if (ModInformation.IsClient) return;

            MapEventInitialize message = new MapEventInitialize(__instance, mapEventType, attackerParty, defenderParty);

            MessageBroker.Instance.Publish(__instance, message);
        }
    }

    [HarmonyPatch(typeof(BattleSimulation))]
    public class BattleSimulationUpdatePatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(BattleSimulation.SimulateBattle))]
        static bool PrefixUpdate(BattleSimulation __instance)
        {
            return false;
        }
    }
}