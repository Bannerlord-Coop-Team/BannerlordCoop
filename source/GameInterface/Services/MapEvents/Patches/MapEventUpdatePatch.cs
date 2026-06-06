using Common;
using GameInterface.Policies;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch(typeof(MapEvent))]
public class MapEventUpdatePatch
{

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