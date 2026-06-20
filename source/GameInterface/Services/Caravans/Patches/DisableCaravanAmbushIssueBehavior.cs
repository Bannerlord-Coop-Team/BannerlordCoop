using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Caravans.Patches;

[HarmonyPatch(typeof(CaravanAmbushIssueBehavior))]
internal class DisableCaravanAmbushIssueBehavior
{
    [HarmonyPatch(nameof(CaravanAmbushIssueBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
