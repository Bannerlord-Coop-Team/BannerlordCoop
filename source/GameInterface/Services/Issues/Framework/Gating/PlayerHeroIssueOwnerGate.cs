using Common;
using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.Heroes.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Framework.Gating;

[HarmonyPatch(typeof(Hero), nameof(Hero.CanHaveCampaignIssues))]
internal class PlayerHeroIssueOwnerGate
{
    [HarmonyPostfix]
    private static void Postfix(Hero __instance, ref bool __result)
    {
        if (__result && ModInformation.IsServer && (__instance.IsPlayerHero() || __instance.Clan.IsPlayerClan()))
        {
            __result = false;
        }
    }
}
