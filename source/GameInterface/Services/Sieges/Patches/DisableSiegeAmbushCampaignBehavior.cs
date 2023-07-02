using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Sieges.Patches;

[HarmonyPatch(typeof(SiegeAmbushCampaignBehavior))]
internal class DisableSiegeAmbushCampaignBehavior
{
    [HarmonyPatch(nameof(SiegeAmbushCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
