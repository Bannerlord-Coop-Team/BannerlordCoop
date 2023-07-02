using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Players.Patches;

[HarmonyPatch(typeof(PlayerCaptivityCampaignBehavior))]
internal class DisablePlayerCaptivityCampaignBehavior
{
    [HarmonyPatch(nameof(PlayerCaptivityCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
