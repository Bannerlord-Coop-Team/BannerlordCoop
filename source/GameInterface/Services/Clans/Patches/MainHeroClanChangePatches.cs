using Common;
using GameInterface.Services.Heroes.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.Clans.Patches;

/// <summary>
/// Redraws the local map's relations when the local player's hero changes clan, which vanilla never does.
/// </summary>
[HarmonyPatch]
internal static class MainHeroClanChangePatches
{
    /// <summary>
    /// Vanilla map views only refresh relations on war, peace and kingdom changes, so a clan join left them stale.
    /// </summary>
    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnHeroChangedClan))]
    [HarmonyPostfix]
    public static void HeroChangedClanPostfix(Hero hero, Clan oldClan)
    {
        if (ModInformation.IsServer || hero.CharacterObject != Game.Current?.PlayerTroop) return;
        if (!ContainerProvider.TryResolve<IMapRelationsInterface>(out var mapRelations)) return;

        mapRelations.RefreshRelations(oldClan?.MapFaction, hero.MapFaction);
    }
}
