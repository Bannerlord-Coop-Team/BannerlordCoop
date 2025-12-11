using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface;

namespace GameInterface.Services.Heroes.Patches.Disable;

[HarmonyPatch(typeof(RomanceCampaignBehavior))]
internal class DisableRomanceCampaignBehavior
{
    [HarmonyPatch(nameof(RomanceCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}
