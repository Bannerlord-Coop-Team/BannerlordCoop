using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.SiegeEvents.Patches;

/// <summary>
/// The siege tick (strategy, construction, bombardment, removal) is server-authoritative; its RNG and
/// object creation diverge if a client runs it over the replicated siege events in its manager list.
/// Clients receive every tick outcome as sync messages and remove ended sieges via the registry destroy.
/// </summary>
[HarmonyPatch(typeof(SiegeEventManager))]
internal class SiegeEventManagerTickPatch
{
    [HarmonyPatch(nameof(SiegeEventManager.Tick))]
    [HarmonyPrefix]
    private static bool TickPrefix()
    {
        return ModInformation.IsServer;
    }
}
