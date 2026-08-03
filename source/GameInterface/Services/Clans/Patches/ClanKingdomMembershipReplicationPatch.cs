using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.Kingdoms.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.Clans.Patches;

/// <summary>
/// Republishes kingdom membership collections after any server-side kingdom change.
/// </summary>
/// <remarks>
/// <see cref="Clan"/>._kingdom is AutoSynced, but the kingdom's own <c>_clans</c>, <c>_fiefsCache</c>,
/// <c>_townsCache</c> and <c>_settlementsCache</c> are not reliably intercepted. Without this, clients
/// agree the clan changed sides while the kingdom's roster still lists it in the old kingdom - the
/// kingdom screen and every Kingdom.Clans consumer stay stale until a reload.
///
/// Individual call sites used to fix this one at a time, so each new membership path (expel, leave,
/// rebellion, clan destruction, a host-initiated defection that never reaches LordBarterHandler) had
/// to remember to do it, and most did not. Every one of them funnels through ApplyInternal, so the
/// republish belongs here rather than in each caller.
///
/// MoveClanToKingdom and KingdomCollectionSync.AddClan are idempotent, so call sites that already
/// republish explicitly stay correct.
/// </remarks>
[HarmonyPatch(typeof(ChangeKingdomAction), "ApplyInternal")]
internal class ClanKingdomMembershipReplicationPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<ClanKingdomMembershipReplicationPatch>();

    [HarmonyPrefix]
    private static void Prefix(Clan clan, out Kingdom __state)
    {
        // Captured before the action mutates it; the postfix needs the kingdom being left.
        __state = clan?.Kingdom;
    }

    [HarmonyPostfix]
    private static void Postfix(Clan clan, Kingdom __state)
    {
        if (!ModInformation.IsServer || clan == null) return;

        var newKingdom = clan.Kingdom;
        if (ReferenceEquals(newKingdom, __state)) return;

        if (!ContainerProvider.TryResolve<IKingdomMembershipState>(out var kingdomMembershipState))
        {
            Logger.Error("Kingdom membership changed for clan {Clan} before the membership state was available",
                clan.StringId);
            return;
        }

        kingdomMembershipState.MoveClanToKingdom(
            __state,
            newKingdom,
            clan,
            publishCollectionChanges: true,
            republishExistingCollections: true);

        // The kingdom's collections are handled above, but Clan._kingdom itself is an AutoSynced
        // reference field and AutoSync cannot send a null one (it keys on the object id), so a clan
        // that left, was expelled or rebelled stayed a member on every client. Send it explicitly.
        MessageBroker.Instance.Publish(clan, new ClanKingdomMembershipChanged(clan, newKingdom));
    }
}
