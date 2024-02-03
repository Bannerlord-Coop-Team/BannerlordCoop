using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Armies.Patches.Disable;

[HarmonyPatch(typeof(PlayerArmyWaitBehavior))]
internal class DisablePlayerArmyWaitBehavior
{
    [HarmonyPatch(nameof(PlayerArmyWaitBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
