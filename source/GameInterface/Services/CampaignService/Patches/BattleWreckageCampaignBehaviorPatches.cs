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

// Goes with BattleWreckageCampaignBehaviorPatches: drops every saved wreckage before the object lists are
// built, since with RegisterEvents skipped they never expire and their menu is never registered.
[HarmonyPatch(typeof(CampaignObjectManager), nameof(CampaignObjectManager.InitializeOnLoad))]
internal class SavedWreckageRemovalPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<SavedWreckageRemovalPatch>();

    // Removes the saved wreckages and logs how many were dropped.
    [HarmonyPrefix]
    private static void Prefix(CampaignObjectManager __instance)
    {
        var removed = RemoveSavedWreckages(__instance);
        if (removed > 0) Logger.Information("Dropped {Count} saved battle wreckages", removed);
    }

    // Clears the saved wreckage list and returns how many entries it held.
    internal static int RemoveSavedWreckages(CampaignObjectManager manager)
    {
        var count = manager._wreckages.Count;
        manager._wreckages.Clear();
        return count;
    }
}
