using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(PlayerTrackCompanionBehavior))]
internal class DisablePlayerTrackCompanionBehavior
{
    [HarmonyPatch(nameof(PlayerTrackCompanionBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
