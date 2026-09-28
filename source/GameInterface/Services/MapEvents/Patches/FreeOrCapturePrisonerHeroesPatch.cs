using Common.Logging;
using GameInterface.Services.MapEvents.Interfaces;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Encounters;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Releases other players' heroes and companions before native picks a hero for the free prisoner
/// conversation. Native reopens that conversation every update while the hero is still a prisoner,
/// and its only line for another clan's companion does not free them.
/// </summary>
[HarmonyPatch(typeof(PlayerEncounter))]
internal class FreeOrCapturePrisonerHeroesPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<FreeOrCapturePrisonerHeroesPatch>();

    [HarmonyPatch(nameof(PlayerEncounter.DoFreeOrCapturePrisonerHeroes))]
    [HarmonyPrefix]
    private static void Prefix(PlayerEncounter __instance)
    {
        if (!ContainerProvider.TryResolve<IPlayerEncounterInterface>(out var playerEncounterInterface)) return;

        try
        {
            playerEncounterInterface.ReleaseHeroesWithoutConversation(__instance);
        }
        catch (Exception e)
        {
            // This runs every update, so fall back to native rather than throwing each frame.
            Logger.Error(e, "Failed to release heroes before the free prisoner conversation");
        }
    }
}
