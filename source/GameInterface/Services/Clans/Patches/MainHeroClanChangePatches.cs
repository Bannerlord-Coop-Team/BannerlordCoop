using Common;
using GameInterface.Services.PartyVisuals.Interfaces;
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
    /// Redraws the local map's stance markers when the local player's hero changes clan.
    /// </summary>
    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnHeroChangedClan))]
    [HarmonyPostfix]
    public static void HeroChangedClanPostfix(Hero hero, Clan oldClan)
    {
        if (ModInformation.IsServer) return;
        // Compares PlayerTroop because Hero.MainHero throws while PlayerTroop is still unset during a join.
        var playerTroop = Game.Current?.PlayerTroop;
        if (playerTroop == null || hero.CharacterObject != playerTroop) return;
        if (!ContainerProvider.TryResolve<IMapStanceVisualsInterface>(out var stanceVisuals)) return;

        stanceVisuals.RedrawStanceVisuals(oldClan?.MapFaction);
    }
}
