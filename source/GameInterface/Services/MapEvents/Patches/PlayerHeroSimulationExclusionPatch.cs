using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Keeps connected players' heroes out of auto-resolve simulations entirely.
/// </summary>
/// <remarks>
/// Vanilla never lets the player's own hero take auto-resolve casualties: the troop supplier excludes
/// <c>CharacterObject.IsPlayerCharacter</c> (i.e. <c>Hero.MainHero</c>) whenever the simulation runs
/// (<c>includePlayer == false</c>). That check is single-player only — on the server a remote client's
/// hero is not the main character, so it gets pulled into the simulation and can be wounded or killed,
/// which (combined with <c>KillCharacterAction</c> also removing the dead hero) drives the party roster
/// negative.
///
/// We mirror the vanilla exclusion for every registered player's main hero by removing them from the
/// prioritized spawn list the model builds. Patching the public, virtual
/// <c>EnqueueTroopSpawnProbabilitiesAccordingToUnitSpawnPrioritization</c> (rather than the private,
/// inline-prone <c>CanTroopJoinBattle</c>) makes the exclusion reliable.
/// </remarks>
[HarmonyPatch(typeof(DefaultTroopSupplierProbabilityModel),
    nameof(DefaultTroopSupplierProbabilityModel.EnqueueTroopSpawnProbabilitiesAccordingToUnitSpawnPrioritization))]
internal class PlayerHeroSimulationExclusionPatch
{
    [HarmonyPostfix]
    private static void Postfix(List<(FlattenedTroopRosterElement, MapEventParty, float)> priorityList, bool includePlayer)
    {
        // includePlayer is true for real missions (players fight) and false for auto-resolve.
        if (includePlayer || priorityList == null)
            return;

        priorityList.RemoveAll(entry => IsPlayerMainHero(entry.Item1));
    }

    private static bool IsPlayerMainHero(FlattenedTroopRosterElement element)
    {
        var hero = element.Troop?.HeroObject;
        var party = hero?.PartyBelongedTo;

        // Each connected player leads their own party; exclude that hero the same way vanilla excludes
        // the main character, while leaving companions to participate as they do in single-player.
        return party != null && party.LeaderHero == hero && party.IsPlayerParty();
    }
}
