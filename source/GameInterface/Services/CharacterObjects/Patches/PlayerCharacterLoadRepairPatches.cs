using Common.Logging;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.CharacterObjects.Patches;

/// <summary>
/// Runs the joined player character repairs around the engine's after-load refill (#4675).
/// </summary>
[HarmonyPatch(typeof(SandBoxManager), nameof(SandBoxManager.InitializeCharactersAfterLoad))]
internal class PlayerCharacterLoadRepairPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<PlayerCharacterLoadRepairPatches>();

    // A prefix, so the engine's refill loop in this method fills the characters it links
    [HarmonyPrefix]
    private static void InitializeCharactersAfterLoadPrefix(bool isSavedCampaign)
    {
        if (!isSavedCampaign) return;
        if (!ContainerProvider.TryResolve<IPlayerCharacterLoadRepairer>(out var repairer)) return;

        try
        {
            repairer.RepairLoadedPlayers(LoadedHeroes(), PlayerCharacterTemplate.Find());
        }
        catch (Exception e)
        {
            // A throw can leave the repair partial; it must not stop the load
            Logger.Error(e, "Repairing joined player characters after a load failed");
        }
    }

    // A postfix, because the refill copies main_hero's culture into every character linked to it
    [HarmonyPostfix]
    private static void InitializeCharactersAfterLoadPostfix(bool isSavedCampaign)
    {
        if (!isSavedCampaign) return;
        if (!ContainerProvider.TryResolve<IPlayerCharacterLoadRepairer>(out var repairer)) return;

        try
        {
            repairer.RestoreHeroCultures(LoadedHeroes(), PlayerCharacterTemplate.Find());
        }
        catch (Exception e)
        {
            Logger.Error(e, "Restoring hero cultures after a load failed");
        }
    }

    private static List<Hero> LoadedHeroes() =>
        Campaign.Current.AliveHeroes.Concat(Campaign.Current.DeadOrDisabledHeroes).ToList();
}
