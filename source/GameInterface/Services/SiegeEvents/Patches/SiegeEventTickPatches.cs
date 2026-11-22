using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.SiegeEvents.Patches;

/// <summary>
/// Keeps the siege tick and its siege-action funnel on the server; clients receive every outcome as sync messages.
/// </summary>
[HarmonyPatch]
internal class SiegeEventTickPatches
{
    // The siege tick (strategy, construction, bombardment, removal) diverges on RNG and object creation if a
    // client runs it over the replicated siege events in its manager list; ended sieges leave via the registry destroy.
    [HarmonyPatch(typeof(SiegeEventManager), nameof(SiegeEventManager.Tick))]
    [HarmonyPrefix]
    private static bool TickPrefix() => ModInformation.IsServer;

    // Second guard behind the tick: a client running the AI's engine construct/deploy/move funnel builds ghost
    // engines with no network id, which never receive hitpoint or destruction sync and desync the whole siege.
    [HarmonyPatch(typeof(SiegeEvent), nameof(SiegeEvent.DoSiegeAction))]
    [HarmonyPrefix]
    private static bool DoSiegeActionPrefix() => ModInformation.IsServer;
}
