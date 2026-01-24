using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Bandits.Patches;

[HarmonyPatch(typeof(BanditSpawnCampaignBehavior))]
internal class DisableBanditsCampaignBehavior
{
    [HarmonyPatch(nameof(BanditSpawnCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
