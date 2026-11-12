using Common.Logging;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.CampaignService.Patches;

[HarmonyPatch(typeof(BattleWreckageCampaignBehavior))]
internal class BattleWreckageCampaignBehaviorPatches
{
    // Off on the server too: co-op drops a server-made wreckage's StringId, which throws in AddWreckage.
    [HarmonyPatch(nameof(BattleWreckageCampaignBehavior.RegisterEvents))]
    [HarmonyPrefix]
    internal static bool RegisterEventsPrefix() => false;
}

// Vanilla's object lists throw on wreckages saved without an id, and OnGameLoaded runs too late to drop them.
[HarmonyPatch(typeof(CampaignObjectManager), nameof(CampaignObjectManager.InitializeOnLoad))]
internal class IdlessWreckageLoadRepairPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<IdlessWreckageLoadRepairPatch>();

    [HarmonyPrefix]
    private static void Prefix(CampaignObjectManager __instance)
    {
        var removed = RemoveIdlessWreckages(__instance);
        if (removed > 0) Logger.Warning("Dropped {Count} battle wreckages saved without an id", removed);
    }

    internal static int RemoveIdlessWreckages(CampaignObjectManager manager) =>
        manager._wreckages?.RemoveAll(wreckage => wreckage == null || wreckage.StringId == null) ?? 0;
}
