using Common.Logging;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.CampaignService.Patches;

// Battle wreckages (the land battle sites and sea wreckages left after a battle) have no registry or sync yet.
[HarmonyPatch(typeof(BattleWreckageCampaignBehavior))]
internal class BattleWreckageCampaignBehaviorPatches
{
    // Off on the server too: co-op drops a server-made wreckage's StringId, which throws in AddWreckage.
    [HarmonyPatch(nameof(BattleWreckageCampaignBehavior.RegisterEvents))]
    [HarmonyPrefix]
    internal static bool RegisterEventsPrefix() => false;
}

// Goes with BattleWreckageCampaignBehaviorPatches. Drops saved wreckages here, since OnGameLoaded is too late.
// Id-less ones throw while the lists are built. Valid ones never expire, and clicking one opens an unregistered menu.
[HarmonyPatch(typeof(CampaignObjectManager), nameof(CampaignObjectManager.InitializeOnLoad))]
internal class SavedWreckageRemovalPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<SavedWreckageRemovalPatch>();

    // Removes the saved wreckages and logs how many were dropped.
    [HarmonyPrefix]
    private static void Prefix(CampaignObjectManager __instance)
    {
        var count = __instance._wreckages.Count;
        __instance._wreckages.Clear();
        if (count > 0) Logger.Information("Dropped {Count} saved battle wreckages", count);
    }
}
