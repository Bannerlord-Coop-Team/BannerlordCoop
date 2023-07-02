using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Towns.Patches;

[HarmonyPatch(typeof(TradeCampaignBehavior))]
internal class DisableTradeCampaignBehavior
{
    [HarmonyPatch(nameof(TradeCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
