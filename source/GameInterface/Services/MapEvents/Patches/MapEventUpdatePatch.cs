using Common;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents.Patches
{
    [HarmonyPatch(typeof(MapEvent))]
    public class MapEventUpdatePatch
    {
        [HarmonyPrefix]
        [HarmonyPatch("Update")]
        static bool PrefixUpdate(MapEvent __instance)
        {
            //To keep client "up to date" without running the Update method
            __instance.RecalculateStrengthOfSides();

            if (ModInformation.IsClient) return false;

            // Don't update if a player is involved
            // Prevents server from instantly finishing the battle and waits for client finish request
            if (__instance.InvolvedParties.Any(x => x.MobileParty.IsPartyControlled() == false)) return false;

            return true;
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