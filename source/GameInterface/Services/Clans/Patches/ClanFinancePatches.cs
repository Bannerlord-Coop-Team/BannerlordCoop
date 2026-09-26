using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch]
internal static class ClanFinancePatches
{
    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnHeroChangedClan))]
    [HarmonyPostfix]
    public static void HeroChangedClanPostfix(Hero hero, Clan oldClan)
    {
        if (ModInformation.IsServer && hero.Clan != oldClan && ContainerProvider.TryResolve<IClanFinance>(out var finance))
        {
            finance.Clear(hero);
        }
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnClanLeaderChanged))]
    [HarmonyPostfix]
    public static void ClanLeaderChangedPostfix(Hero oldLeader, Hero newLeader)
    {
        if (ModInformation.IsServer && ContainerProvider.TryResolve<IClanFinance>(out var finance))
        {
            foreach (var hero in newLeader.Clan.Heroes)
            {
                finance.Clear(hero);
            }
        }
    }
}
