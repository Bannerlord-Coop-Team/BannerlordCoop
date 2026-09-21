using Common;
using HarmonyLib;
using SandBox.CampaignBehaviors;

namespace GameInterface.Services.Players.Patches;

[HarmonyPatch(typeof(RetirementCampaignBehavior))]
internal class RetirementCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(RetirementCampaignBehavior.OnSessionLaunched))]
    [HarmonyPrefix]
    public static bool OnSessionLaunchedPrefix() => ModInformation.IsClient;

    [HarmonyPatch(nameof(RetirementCampaignBehavior.HourlyTick))]
    [HarmonyPrefix]
    public static bool HourlyTickPrefix() => ModInformation.IsClient;

    [HarmonyPatch(nameof(RetirementCampaignBehavior.GameMenuOpened))]
    [HarmonyPrefix]
    public static bool GameMenuOpenedPrefix() => ModInformation.IsClient;
}
