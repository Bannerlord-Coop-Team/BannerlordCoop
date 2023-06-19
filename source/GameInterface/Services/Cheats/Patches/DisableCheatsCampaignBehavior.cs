using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Cheats.Patches;

[HarmonyPatch(typeof(CheatsCampaignBehavior))]
internal class DisableCheatsCampaignBehavior
{
    [HarmonyPatch(nameof(CheatsCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
