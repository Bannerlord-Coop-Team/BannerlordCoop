using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(RaidAnEnemyTerritoryIssueBehavior))]
internal class DisableRaidAnEnemyTerritoryIssueBehavior
{
    [HarmonyPatch(nameof(RaidAnEnemyTerritoryIssueBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
