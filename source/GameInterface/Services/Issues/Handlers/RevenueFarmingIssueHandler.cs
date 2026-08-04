using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Issues.Handlers;

/// <summary>
/// Server-authoritative Revenue Farming issue replication - mirrors <see cref="VillageNeedsCraftingMaterialsIssueHandler"/>'s
/// creation/acceptance routing shape (same genuine-accept-runs-locally-first, server-arbitrates-races,
/// never-trust-a-client-claimed-ControllerId pattern), but with its own message types (see
/// Messages/RevenueFarmingIssueMessages.cs's per-type doc comments for why they can't be shared wire types) and
/// its own accept-time force-write mechanism (see <see cref="IRevenueFarmingIssueInterface"/>'s type doc comment
/// for the central "why" - this issue type's <c>_revenueVillages</c>/<c>_totalRequestedDenars</c> are re-derived
/// per-client at ACCEPT time, not frozen at creation).
///
/// This type has no alternative solution (<c>IsThereAlternativeSolution == false</c>), so there is no
/// alternative-solution-accept subscription pair at all here, unlike <see cref="VillageNeedsCraftingMaterialsIssueHandler"/>.
///
/// Deliberately does NOT subscribe to a finalize/removal message of its own - <c>VillageIssueFinalizedTriggered</c>/
/// <c>RequestVillageIssueRemoved</c>/<c>NetworkVillageIssueRemoved</c> (all in VillageNeedsToolsIssueMessages.cs)
/// and <see cref="VillageNeedsToolsIssueHandler"/>'s own handlers for them are already fully generic (see
/// <see cref="Patches.IssueFinalizedPatches"/>/<c>Interfaces.VillageNeedsToolsIssueInterface.FinalizeMirror</c>,
/// both dispatch on the shared <c>IssueBase</c>/<c>QuestBase</c> types alone, gated only by
/// <see cref="Patches.DisableAllIssueBehaviorsExceptAllowlist.IsAllowlisted"/>) - once this issue type joins the
/// allowlist, that existing, unmodified machinery already correctly tears down a Revenue Farming issue on every
/// peer. Adding a second, parallel subscription here would just double the broadcast for every finalize without
/// adding correctness - same reasoning as every other type in this family.
/// </summary>
internal class RevenueFarmingIssueHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<RevenueFarmingIssueHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IRevenueFarmingIssueInterface issueInterface;
    private readonly IPlayerManager playerManager;

    public RevenueFarmingIssueHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IRevenueFarmingIssueInterface issueInterface,
        IPlayerManager playerManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.issueInterface = issueInterface;
        this.playerManager = playerManager;

        messageBroker.Subscribe<RevenueFarmingIssueCreated>(Handle_RevenueFarmingIssueCreated);
        messageBroker.Subscribe<NetworkRevenueFarmingIssueCreated>(Handle_NetworkRevenueFarmingIssueCreated);

        messageBroker.Subscribe<RevenueFarmingQuestAcceptTriggered>(Handle_RevenueFarmingQuestAcceptTriggered);
        messageBroker.Subscribe<RequestRevenueFarmingIssueAcceptQuest>(Handle_RequestRevenueFarmingIssueAcceptQuest);
        messageBroker.Subscribe<NetworkRevenueFarmingIssueQuestAccepted>(Handle_NetworkRevenueFarmingIssueQuestAccepted);

        messageBroker.Subscribe<NetworkRevenueFarmingIssueAcceptRejected>(Handle_NetworkRevenueFarmingIssueAcceptRejected);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<RevenueFarmingIssueCreated>(Handle_RevenueFarmingIssueCreated);
        messageBroker.Unsubscribe<NetworkRevenueFarmingIssueCreated>(Handle_NetworkRevenueFarmingIssueCreated);

        messageBroker.Unsubscribe<RevenueFarmingQuestAcceptTriggered>(Handle_RevenueFarmingQuestAcceptTriggered);
        messageBroker.Unsubscribe<RequestRevenueFarmingIssueAcceptQuest>(Handle_RequestRevenueFarmingIssueAcceptQuest);
        messageBroker.Unsubscribe<NetworkRevenueFarmingIssueQuestAccepted>(Handle_NetworkRevenueFarmingIssueQuestAccepted);

        messageBroker.Unsubscribe<NetworkRevenueFarmingIssueAcceptRejected>(Handle_NetworkRevenueFarmingIssueAcceptRejected);
    }

    // --- Creation: server rolls once, broadcasts the resolved target settlement ---

    private void Handle_RevenueFarmingIssueCreated(MessagePayload<RevenueFarmingIssueCreated> payload)
    {
        if (ModInformation.IsClient) return;

        var issue = payload.What.Issue;
        if (issue?.IssueOwner == null) return;
        if (!objectManager.TryGetIdWithLogging(issue.IssueOwner, out var ownerId)) return;

        if (!issueInterface.TryCaptureFields(issue, out var targetSettlement))
        {
            Logger.Error("Could not capture Revenue Farming issue fields for owner {Owner}", ownerId);
            return;
        }

        if (!objectManager.TryGetIdWithLogging(targetSettlement, out var targetSettlementId)) return;

        network.SendAll(new NetworkRevenueFarmingIssueCreated(ownerId, targetSettlementId));
    }

    private void Handle_NetworkRevenueFarmingIssueCreated(MessagePayload<NetworkRevenueFarmingIssueCreated> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;

            // Idempotent: a resend (or the full save transfer at join already having restored this via the
            // normal savegame) must not create a second issue for the same hero.
            if (owner.Issue != null) return;

            if (!objectManager.TryGetObjectWithLogging<Settlement>(data.TargetSettlementId, out var targetSettlement)) return;

            var replicated = issueInterface.ConstructReplicated(owner, targetSettlement);

            issueInterface.RegisterReplicated(owner, replicated);
        });
    }

    // --- Acceptance: the accepting machine already applied it locally for real; the server arbitrates a
    // same-issue double-accept race and confirms/rejects it - and resolves/broadcasts the authoritative
    // _totalRequestedDenars/_revenueVillages every peer force-corrects to. ---

    private void Handle_RevenueFarmingQuestAcceptTriggered(MessagePayload<RevenueFarmingQuestAcceptTriggered> payload)
    {
        var owner = payload.What.Owner;
        if (owner == null || !objectManager.TryGetIdWithLogging(owner, out var ownerId)) return;

        if (ModInformation.IsServer)
        {
            // The host's own live conversation just accepted - already authoritative, including the
            // total/villages it captured at the same instant (see Patches.RevenueFarmingQuestAcceptancePatch).
            // Record ownership locally too, exactly like every other peer will when this broadcast comes back.
            var hostControllerId = payload.What.ControllerId;
            if (!TryPackVillages(payload.What.Villages, out var wireVillages))
            {
                Logger.Error("Could not resolve settlement ids for owner {Owner}'s own accepted revenue villages", ownerId);
                return;
            }

            VillageNeedsToolsIssueOwnership.SetOwner(owner, hostControllerId);
            network.SendAll(new NetworkRevenueFarmingIssueQuestAccepted(
                ownerId, hostControllerId, payload.What.TotalRequestedDenars, wireVillages));
        }
        else
        {
            // A client's own live conversation already applied this locally with its own (not yet
            // authoritative) terms - tell the server so it can arbitrate a same-issue double-accept race and
            // resolve the real terms itself. Deliberately no total/villages/ControllerId fields here: none of
            // this client's own locally-rolled values can be trusted as authoritative (see the interface's
            // type doc comment) - the server re-derives its own via ReplayQuestAccepted below.
            network.SendAll(new RequestRevenueFarmingIssueAcceptQuest(ownerId));
        }
    }

    private void Handle_RequestRevenueFarmingIssueAcceptQuest(MessagePayload<RequestRevenueFarmingIssueAcceptQuest> payload)
    {
        if (ModInformation.IsClient) return;

        var ownerId = payload.What.OwnerId;
        var requester = payload.Who as NetPeer;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(ownerId, out var owner)) return;

            if (requester == null || !playerManager.TryGetPlayer(requester, out var player))
            {
                Logger.Error("Rejecting {Message} from an unregistered/unknown requester for owner {Owner}",
                    nameof(RequestRevenueFarmingIssueAcceptQuest), ownerId);
                if (requester != null) network.Send(requester, new NetworkRevenueFarmingIssueAcceptRejected(ownerId));
                return;
            }

            if (owner.Issue is RevenueFarmingIssueBehavior.RevenueFarmingIssue && owner.Issue.IsOngoingWithoutQuest)
            {
                // First valid request wins the race - replay on the server's own authoritative copy, capture
                // what that replay actually baked (the server's own live BoundVillages/raid-state read IS the
                // authoritative one), then confirm those exact values to everyone.
                issueInterface.ReplayQuestAccepted(owner);
                if (!issueInterface.TryCaptureQuestFields(owner, out var totalRequestedDenars, out var villages) ||
                    !TryPackVillages(villages, out var wireVillages))
                {
                    Logger.Error("Replayed StartIssueQuest for owner {Owner} but could not read back its quest fields", ownerId);
                    return;
                }

                VillageNeedsToolsIssueOwnership.SetOwner(owner, player.ControllerId);
                network.SendAll(new NetworkRevenueFarmingIssueQuestAccepted(ownerId, player.ControllerId, totalRequestedDenars, wireVillages));
            }
            else
            {
                // Someone else already won the race (or the issue is gone) - tell just this requester to roll
                // their own already-applied local copy back.
                network.Send(requester, new NetworkRevenueFarmingIssueAcceptRejected(ownerId));
            }
        });
    }

    private void Handle_NetworkRevenueFarmingIssueQuestAccepted(MessagePayload<NetworkRevenueFarmingIssueQuestAccepted> payload)
    {
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;

            if (!TryUnpackVillages(data.Villages, out var villages))
            {
                Logger.Error("Could not resolve settlement ids for owner {Owner}'s incoming accepted revenue villages", data.OwnerId);
                return;
            }

            // Mirror (replaying if this peer has no quest yet) and force-correct the total/villages in the
            // same synchronous block that also records ownership - no TOCTOU window between "quest object
            // exists" and "ownership known", same as every other type's equivalent handler.
            issueInterface.MirrorQuestAccepted(owner, data.TotalRequestedDenars, villages);
            VillageNeedsToolsIssueOwnership.SetOwner(owner, data.OwnerControllerId);
        });
    }

    private void Handle_NetworkRevenueFarmingIssueAcceptRejected(MessagePayload<NetworkRevenueFarmingIssueAcceptRejected> payload)
    {
        var ownerId = payload.What.OwnerId;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(ownerId, out var owner)) return;
            issueInterface.RejectAcceptance(owner);
        });
    }

    // --- Wire <-> domain conversion for the villages list ---

    private bool TryPackVillages(IReadOnlyList<(Settlement Settlement, int TargetAmount)> villages, out RevenueVillageWireEntry[] wireVillages)
    {
        wireVillages = null;
        if (villages == null || villages.Count == 0) return false;

        var packed = new List<RevenueVillageWireEntry>(villages.Count);
        foreach (var (settlement, targetAmount) in villages)
        {
            if (!objectManager.TryGetIdWithLogging(settlement, out var settlementId)) return false;
            packed.Add(new RevenueVillageWireEntry(settlementId, targetAmount));
        }

        wireVillages = packed.ToArray();
        return true;
    }

    private bool TryUnpackVillages(RevenueVillageWireEntry[] wireVillages, out IReadOnlyList<(Settlement Settlement, int TargetAmount)> villages)
    {
        villages = null;
        if (wireVillages == null || wireVillages.Length == 0) return false;

        var unpacked = new List<(Settlement Settlement, int TargetAmount)>(wireVillages.Length);
        foreach (var entry in wireVillages)
        {
            if (!objectManager.TryGetObjectWithLogging<Settlement>(entry.SettlementId, out var settlement)) return false;
            unpacked.Add((settlement, entry.TargetAmount));
        }

        villages = unpacked;
        return true;
    }
}
