using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(RomanceCampaignBehavior))]
internal class DisableRomanceCampaignBehavior
{
    [HarmonyPatch(nameof(RomanceCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
