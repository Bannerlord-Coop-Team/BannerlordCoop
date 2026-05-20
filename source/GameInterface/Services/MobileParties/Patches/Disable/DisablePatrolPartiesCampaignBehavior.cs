using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// Disables <see cref="PatrolPartiesCampaignBehavior"/> on the CLIENT only.
///
/// The server must run this behavior normally: it spawns patrol parties via
/// <c>SpawnPatrolParty</c>, ticks their AI, replenishes their rosters, and destroys
/// them when appropriate. All of those actions flow through the coop sync pipeline
/// (party creation → <c>NetworkCreatePartyComponent</c> + <c>NetworkCreateParty</c>,
/// destruction → <c>NetworkDestroyParty</c>, field mutations → DynamicSync).
///
/// The client must NOT run this behavior. If it did, it would independently spawn its
/// own local patrol parties, duplicating the ones already synced from the server and
/// triggering the same NullReferenceException that motivated this patch:
/// <c>MobilePartyDataPatches.SetPartyComponentIntercept</c> would fire, find the
/// newly constructed <c>PatrolPartyComponent</c> in the registry (it is now registered),
/// publish a <c>PartyComponentChanged</c> event, and the client-side party would
/// incorrectly broadcast as if it were a server-authoritative change.
///
/// Clients receive patrol parties exclusively through the sync pipeline; this patch
/// simply prevents the behavior from subscribing to any campaign events on the client.
/// </summary>
[HarmonyPatch(typeof(PatrolPartiesCampaignBehavior))]
internal class DisablePatrolPartiesCampaignBehavior
{
    [HarmonyPatch(nameof(PatrolPartiesCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}
