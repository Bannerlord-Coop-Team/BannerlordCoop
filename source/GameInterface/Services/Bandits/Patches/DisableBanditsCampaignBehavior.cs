using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.Bandits.Patches;

[HarmonyPatch(typeof(BanditSpawnCampaignBehavior))]
internal class DisableBanditsCampaignBehavior
{
    [HarmonyPatch(nameof(BanditSpawnCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return ModInformation.IsServer;
    }
}
