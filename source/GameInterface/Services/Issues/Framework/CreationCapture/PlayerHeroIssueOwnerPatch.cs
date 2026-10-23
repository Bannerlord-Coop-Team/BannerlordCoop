using Common;
using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.Heroes.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Framework.CreationCapture;

/// <summary>
/// Vanilla only stops <c>Clan.PlayerClan</c> from offering issues, which on the server is not a player, so this stops every player hero and their clan.
/// </summary>
[HarmonyPatch(typeof(Hero), nameof(Hero.CanHaveCampaignIssues))]
internal class PlayerHeroIssueOwnerPatch
{
    /// <summary>Reports that a player hero or a hero of a player clan cannot have issues on the server.</summary>
    [HarmonyPostfix]
    private static void Postfix(Hero __instance, ref bool __result)
    {
        if (__result && ModInformation.IsServer && (__instance.IsPlayerHero() || __instance.Clan.IsPlayerClan()))
        {
            __result = false;
        }
    }
}
