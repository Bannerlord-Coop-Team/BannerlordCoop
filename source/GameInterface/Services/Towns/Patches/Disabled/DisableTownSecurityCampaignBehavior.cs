using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Towns.Patches.Disabled;

[HarmonyPatch(typeof(TownSecurityCampaignBehavior))]
internal class DisableTownSecurityCampaignBehavior
{
    [HarmonyPatch(nameof(TownSecurityCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
