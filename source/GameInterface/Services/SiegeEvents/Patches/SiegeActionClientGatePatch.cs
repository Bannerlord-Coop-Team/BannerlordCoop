using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.SiegeEvents.Patches;

/// <summary>
/// DoSiegeAction is the siege AI's funnel for constructing, deploying, and moving engines. Only the server
/// runs it authoritatively; its create and deploy side effects replicate to clients through the auto-registry
/// and the container patches. A client running it builds ghost engines with no network id, which then never
/// receive hitpoint or destruction sync and desync the whole siege, so gate it to the server.
/// </summary>
[HarmonyPatch(typeof(SiegeEvent), nameof(SiegeEvent.DoSiegeAction))]
internal class SiegeActionClientGatePatch
{
    [HarmonyPrefix]
    private static bool Prefix() => ModInformation.IsServer;
}
