using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Bandits.Patches;

/// <summary>
/// Does NOT skip event registration. This makes all bandits enemies of
/// all other factions at the start of the game
/// </summary>
[HarmonyPatch(typeof(OutlawClansCampaignBehavior))]
internal class DisableOutlawClansCampaignBehavior
{
    [HarmonyPatch(nameof(OutlawClansCampaignBehavior.RegisterEvents))]
    static bool Prefix() => true;
}
