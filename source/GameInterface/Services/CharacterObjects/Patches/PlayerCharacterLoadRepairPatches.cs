using Common.Logging;
using HarmonyLib;
using Serilog;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.CharacterObjects.Patches;

/// <summary>
/// Repairs joined players' characters after a load (#4675): links the ones a save made before the template link
/// left without one, and gives every character linked to the template its hero's culture.
/// </summary>
[HarmonyPatch(typeof(SandBoxManager))]
internal class PlayerCharacterLoadRepairPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<PlayerCharacterLoadRepairPatches>();

    // A prefix, so the engine's refill loop in this method fills the characters it links
    [HarmonyPatch(nameof(SandBoxManager.InitializeCharactersAfterLoad))]
    [HarmonyPrefix]
    private static void InitializeCharactersAfterLoadPrefix(bool isSavedCampaign)
    {
        if (!isSavedCampaign) return;

        try
        {
            var heroes = Campaign.Current.AliveHeroes.Concat(Campaign.Current.DeadOrDisabledHeroes).ToList();
            int linked = PlayerCharacterTemplate.RepairLoadedPlayers(heroes, PlayerCharacterTemplate.Find());

            if (linked > 0)
            {
                Logger.Information(
                    "Linked {Count} joined player character(s) without a template link to {TemplateId} so the load refills them (#4675)",
                    linked,
                    PlayerCharacterTemplate.TemplateId);
            }
        }
        catch (Exception e)
        {
            // A throw can leave the repair partial; it must not stop the load
            Logger.Error(e, "Repairing joined player characters after a load failed");
        }
    }

    // A postfix, because the refill copies main_hero's culture into every character linked to it
    [HarmonyPatch(nameof(SandBoxManager.InitializeCharactersAfterLoad))]
    [HarmonyPostfix]
    private static void InitializeCharactersAfterLoadPostfix(bool isSavedCampaign)
    {
        if (!isSavedCampaign) return;

        try
        {
            var heroes = Campaign.Current.AliveHeroes.Concat(Campaign.Current.DeadOrDisabledHeroes).ToList();
            int restored = PlayerCharacterTemplate.RestoreHeroCultures(heroes, PlayerCharacterTemplate.Find());

            if (restored > 0)
            {
                Logger.Information(
                    "Gave {Count} character(s) linked to {TemplateId} their hero's culture after the load refill (#4675)",
                    restored,
                    PlayerCharacterTemplate.TemplateId);
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, "Restoring hero cultures after a load failed");
        }
    }
}
