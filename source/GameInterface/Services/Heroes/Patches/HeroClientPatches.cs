using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(Hero))]
internal class HeroClientPatches
{
    //[HarmonyPatch(nameof(Hero.UpdateHomeSettlement))]
    //[HarmonyPrefix]
    //private static bool DisableIfClient() => ModInformation.IsServer;
}
