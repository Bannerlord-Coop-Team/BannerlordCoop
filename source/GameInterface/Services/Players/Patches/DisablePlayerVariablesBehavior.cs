using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Players.Patches;

[HarmonyPatch(typeof(PlayerVariablesBehavior))]
internal class DisablePlayerVariablesBehavior
{
    [HarmonyPatch(nameof(PlayerVariablesBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
