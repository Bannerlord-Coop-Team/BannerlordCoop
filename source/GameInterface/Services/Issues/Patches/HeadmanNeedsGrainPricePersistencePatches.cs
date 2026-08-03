using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Join/reconnect half of the weekly grain-price mechanism (see
/// <see cref="HeadmanNeedsGrainPriceCachePatches"/> for the ongoing, in-session broadcast half). Piggybacks on
/// <c>HeadmanNeedsGrainIssueBehavior.SyncData</c> (an empty override in vanilla - confirmed against the
/// decompiled source), the exact same pattern <see cref="VillageNeedsToolsIssueOwnershipPersistencePatches"/>
/// already established for a different behavior's save record.
///
/// This is what makes a (re)connecting client come out correct without waiting up to a full in-game week for
/// the next broadcast: per <c>Coop.Core.Server.Connections.States.TransferSaveState</c>, every (re)connection
/// is serviced by the server calling <c>SaveCurrentGame()</c> on its own live state and shipping that snapshot
/// to the connecting peer - so this postfix's <c>IsSaving</c> branch captures the server's own current,
/// authoritative <c>_averageGrainPriceInCalradia</c> into that snapshot, and the connecting client's own
/// <c>IsLoading</c> branch (running as part of loading that exact snapshot) restores it directly, well before
/// <c>OnGameLoadFinishedEvent</c> would otherwise fire (which is blocked from recomputing locally anyway - see
/// <see cref="HeadmanNeedsGrainPriceCachePatches"/>).
///
/// Uses a plain <c>int</c> via <c>IDataStore.SyncData&lt;T&gt;</c> directly - no custom
/// <c>SaveableTypeDefiner</c> needed (unlike <c>VillageNeedsToolsIssueOwnershipSaveData</c>'s dictionary
/// snapshot), since this is a single primitive value, not a collection of custom records.
/// </summary>
[HarmonyPatch(typeof(HeadmanNeedsGrainIssueBehavior))]
internal class HeadmanNeedsGrainPricePersistencePatches
{
    private const string SaveKey = "_coop_headman_needs_grain_average_price";

    [HarmonyPatch(nameof(HeadmanNeedsGrainIssueBehavior.SyncData))]
    [HarmonyPostfix]
    private static void SyncDataPostfix(HeadmanNeedsGrainIssueBehavior __instance, IDataStore dataStore)
    {
        int price = __instance._averageGrainPriceInCalradia;
        dataStore.SyncData(SaveKey, ref price);

        if (dataStore.IsLoading)
        {
            __instance._averageGrainPriceInCalradia = price;
        }
    }
}
