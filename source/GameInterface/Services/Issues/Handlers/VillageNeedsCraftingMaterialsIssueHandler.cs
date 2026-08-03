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
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Handlers;

/// <summary>
/// Server-authoritative Village Needs Crafting Materials issue replication - mirrors
/// <see cref="VillageNeedsToolsIssueHandler"/>'s creation/acceptance routing closely (same
/// genuine-accept-runs-locally-first, server-arbitrates-races, never-trust-a-client-claimed-ControllerId
/// shape), but with its own message types for creation/accept (see
/// Messages/VillageNeedsCraftingMaterialsIssueMessages.cs's file-level note for why they can't be shared wire
/// types) and its own accept-time force-write mechanism (see
/// <see cref="IVillageNeedsCraftingMaterialsIssueInterface"/>'s type doc comment for the central "why" - this
/// issue type's required-amount/reward are re-rolled per-client at ACCEPT time, unlike Tools' creation-time-only
/// rolls).
///
/// Deliberately does NOT subscribe to a finalize/removal message of its own: <c>VillageIssueFinalizedTriggered</c>/
/// <c>RequestVillageIssueRemoved</c>/<c>NetworkVillageIssueRemoved</c> (all in VillageNeedsToolsIssueMessages.cs)
/// and <see cref="VillageNeedsToolsIssueHandler"/>'s own handlers for them are already fully generic - no
/// Tools-specific type check anywhere in that teardown path, since it only ever calls generic
/// <c>QuestBase.CompleteQuestWithXxx</c>/<c>IssueBase.CompleteIssueWithXxx</c>/bare <c>IssueFinalized()</c> on
/// whichever issue the owning Hero actually has. Once <see cref="Patches.IssueFinalizedPatches"/>'s two type
/// guards are widened to also recognize this issue type (see that file), that existing, unmodified handler
/// already correctly tears down a Crafting Materials issue on every peer. Adding a second, parallel
/// subscription here would just double the <c>SendAll</c> broadcast for EVERY issue finalize (Tools' or this
/// type's) without adding any correctness - a deviation from the original design sketch's literal plan,
/// deliberately made here because the duplication cost only becomes apparent once you trace how many peers
/// react to one already-shared, already-generic local event.
/// </summary>
internal class VillageNeedsCraftingMaterialsIssueHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<VillageNeedsCraftingMaterialsIssueHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IVillageNeedsCraftingMaterialsIssueInterface issueInterface;
    private readonly IPlayerManager playerManager;

    public VillageNeedsCraftingMaterialsIssueHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IVillageNeedsCraftingMaterialsIssueInterface issueInterface,
        IPlayerManager playerManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.issueInterface = issueInterface;
        this.playerManager = playerManager;

        messageBroker.Subscribe<VillageCraftingIssueCreated>(Handle_VillageCraftingIssueCreated);
        messageBroker.Subscribe<NetworkVillageCraftingIssueCreated>(Handle_NetworkVillageCraftingIssueCreated);

        messageBroker.Subscribe<VillageCraftingIssueQuestAcceptTriggered>(Handle_VillageCraftingIssueQuestAcceptTriggered);
        messageBroker.Subscribe<RequestVillageCraftingIssueAcceptQuest>(Handle_RequestVillageCraftingIssueAcceptQuest);
        messageBroker.Subscribe<NetworkVillageCraftingIssueQuestAccepted>(Handle_NetworkVillageCraftingIssueQuestAccepted);

        messageBroker.Subscribe<VillageCraftingIssueAlternativeAcceptTriggered>(Handle_VillageCraftingIssueAlternativeAcceptTriggered);
        messageBroker.Subscribe<RequestVillageCraftingIssueAcceptAlternative>(Handle_RequestVillageCraftingIssueAcceptAlternative);
        messageBroker.Subscribe<NetworkVillageCraftingIssueAlternativeAccepted>(Handle_NetworkVillageCraftingIssueAlternativeAccepted);

        messageBroker.Subscribe<NetworkVillageCraftingIssueAcceptRejected>(Handle_NetworkVillageCraftingIssueAcceptRejected);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<VillageCraftingIssueCreated>(Handle_VillageCraftingIssueCreated);
        messageBroker.Unsubscribe<NetworkVillageCraftingIssueCreated>(Handle_NetworkVillageCraftingIssueCreated);

        messageBroker.Unsubscribe<VillageCraftingIssueQuestAcceptTriggered>(Handle_VillageCraftingIssueQuestAcceptTriggered);
        messageBroker.Unsubscribe<RequestVillageCraftingIssueAcceptQuest>(Handle_RequestVillageCraftingIssueAcceptQuest);
        messageBroker.Unsubscribe<NetworkVillageCraftingIssueQuestAccepted>(Handle_NetworkVillageCraftingIssueQuestAccepted);

        messageBroker.Unsubscribe<VillageCraftingIssueAlternativeAcceptTriggered>(Handle_VillageCraftingIssueAlternativeAcceptTriggered);
        messageBroker.Unsubscribe<RequestVillageCraftingIssueAcceptAlternative>(Handle_RequestVillageCraftingIssueAcceptAlternative);
        messageBroker.Unsubscribe<NetworkVillageCraftingIssueAlternativeAccepted>(Handle_NetworkVillageCraftingIssueAlternativeAccepted);

        messageBroker.Unsubscribe<NetworkVillageCraftingIssueAcceptRejected>(Handle_NetworkVillageCraftingIssueAcceptRejected);
    }

    // --- Creation: server rolls once, broadcasts the resolved requested material ---

    private void Handle_VillageCraftingIssueCreated(MessagePayload<VillageCraftingIssueCreated> payload)
    {
        if (ModInformation.IsClient) return;

        var issue = payload.What.Issue;
        if (issue?.IssueOwner == null) return;
        if (!objectManager.TryGetIdWithLogging(issue.IssueOwner, out var ownerId)) return;

        if (!issueInterface.TryCaptureFields(issue, out var requestedItem))
        {
            Logger.Error("Could not capture Village Needs Crafting Materials issue fields for owner {Owner}", ownerId);
            return;
        }

        if (!objectManager.TryGetIdWithLogging(requestedItem, out var requestedItemId)) return;

        network.SendAll(new NetworkVillageCraftingIssueCreated(ownerId, requestedItemId));
    }

    private void Handle_NetworkVillageCraftingIssueCreated(MessagePayload<NetworkVillageCraftingIssueCreated> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;

            // Idempotent: a resend (or the full save transfer at join already having restored this via the
            // normal savegame) must not create a second issue for the same hero.
            if (owner.Issue != null) return;

            if (!objectManager.TryGetObjectWithLogging<ItemObject>(data.RequestedItemId, out var requestedItem)) return;

            var replicated = issueInterface.ConstructReplicated(owner, requestedItem);

            issueInterface.RegisterReplicated(owner, replicated);
        });
    }

    // --- Acceptance: the accepting machine already applied it locally for real; the server arbitrates a
    // same-issue double-accept race and confirms/rejects it - and, for the quest-solution path, resolves and
    // broadcasts the authoritative required-amount/reward every peer force-corrects to. ---

    private void Handle_VillageCraftingIssueQuestAcceptTriggered(MessagePayload<VillageCraftingIssueQuestAcceptTriggered> payload)
    {
        var owner = payload.What.Owner;
        if (owner == null || !objectManager.TryGetIdWithLogging(owner, out var ownerId)) return;

        if (ModInformation.IsServer)
        {
            // The host's own live conversation just accepted - already authoritative, including the
            // requested-amount/reward it captured at the same instant (see
            // Patches.VillageNeedsCraftingMaterialsAcceptancePatches). Record ownership locally too, exactly
            // like every other peer will when this broadcast comes back to them.
            var hostControllerId = payload.What.ControllerId;
            VillageNeedsToolsIssueOwnership.SetOwner(owner, hostControllerId);
            network.SendAll(new NetworkVillageCraftingIssueQuestAccepted(
                ownerId, hostControllerId, payload.What.RequestedItemAmount, payload.What.RewardGold));
        }
        else
        {
            // A client's own live conversation already applied this locally with its own (not yet
            // authoritative) terms - tell the server so it can arbitrate a same-issue double-accept race and
            // resolve the real terms itself. Deliberately no amount/reward/ControllerId fields here: none of
            // this client's own locally-rolled values can be trusted as authoritative (see the interface's
            // type doc comment) - the server re-derives its own via ReplayQuestAccepted below.
            network.SendAll(new RequestVillageCraftingIssueAcceptQuest(ownerId));
        }
    }

    private void Handle_RequestVillageCraftingIssueAcceptQuest(MessagePayload<RequestVillageCraftingIssueAcceptQuest> payload)
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
                    nameof(RequestVillageCraftingIssueAcceptQuest), ownerId);
                if (requester != null) network.Send(requester, new NetworkVillageCraftingIssueAcceptRejected(ownerId));
                return;
            }

            if (owner.Issue is VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue && owner.Issue.IsOngoingWithoutQuest)
            {
                // First valid request wins the race - replay on the server's own authoritative copy, capture
                // what that replay actually baked (the server's own IssueDifficultyMultiplier/market-price
                // rolls ARE the authoritative ones), then confirm those exact values to everyone.
                issueInterface.ReplayQuestAccepted(owner);
                if (!issueInterface.TryCaptureQuestFields(owner, out var requestedItemAmount, out var rewardGold))
                {
                    Logger.Error("Replayed StartIssueQuest for owner {Owner} but could not read back its quest fields", ownerId);
                    return;
                }

                VillageNeedsToolsIssueOwnership.SetOwner(owner, player.ControllerId);
                network.SendAll(new NetworkVillageCraftingIssueQuestAccepted(ownerId, player.ControllerId, requestedItemAmount, rewardGold));
            }
            else
            {
                // Someone else already won the race (or the issue is gone) - tell just this requester to
                // roll their own already-applied local copy back.
                network.Send(requester, new NetworkVillageCraftingIssueAcceptRejected(ownerId));
            }
        });
    }

    private void Handle_NetworkVillageCraftingIssueQuestAccepted(MessagePayload<NetworkVillageCraftingIssueQuestAccepted> payload)
    {
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;
            // Mirror (replaying if this peer has no quest yet) and force-correct the required-amount/reward
            // in the same synchronous block that also records ownership - no TOCTOU window between "dialogue
            // registered" and "ownership known", same as the Tools handler's equivalent.
            issueInterface.MirrorQuestAccepted(owner, data.RequestedItemAmount, data.RewardGold);
            VillageNeedsToolsIssueOwnership.SetOwner(owner, data.OwnerControllerId);
        });
    }

    private void Handle_VillageCraftingIssueAlternativeAcceptTriggered(MessagePayload<VillageCraftingIssueAlternativeAcceptTriggered> payload)
    {
        var owner = payload.What.Owner;
        if (owner == null || !objectManager.TryGetIdWithLogging(owner, out var ownerId)) return;

        if (ModInformation.IsServer)
        {
            var hostControllerId = payload.What.ControllerId;
            VillageNeedsToolsIssueOwnership.SetOwner(owner, hostControllerId);
            network.SendAll(new NetworkVillageCraftingIssueAlternativeAccepted(ownerId, hostControllerId));
        }
        else
        {
            network.SendAll(new RequestVillageCraftingIssueAcceptAlternative(ownerId));
        }
    }

    private void Handle_RequestVillageCraftingIssueAcceptAlternative(MessagePayload<RequestVillageCraftingIssueAcceptAlternative> payload)
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
                    nameof(RequestVillageCraftingIssueAcceptAlternative), ownerId);
                if (requester != null) network.Send(requester, new NetworkVillageCraftingIssueAcceptRejected(ownerId));
                return;
            }

            if (owner.Issue is VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue && owner.Issue.IsOngoingWithoutQuest)
            {
                issueInterface.MirrorAlternativeAccepted(owner);
                VillageNeedsToolsIssueOwnership.SetOwner(owner, player.ControllerId);
                network.SendAll(new NetworkVillageCraftingIssueAlternativeAccepted(ownerId, player.ControllerId));
            }
            else
            {
                network.Send(requester, new NetworkVillageCraftingIssueAcceptRejected(ownerId));
            }
        });
    }

    private void Handle_NetworkVillageCraftingIssueAlternativeAccepted(MessagePayload<NetworkVillageCraftingIssueAlternativeAccepted> payload)
    {
        var ownerId = payload.What.OwnerId;
        var ownerControllerId = payload.What.OwnerControllerId;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(ownerId, out var owner)) return;
            issueInterface.MirrorAlternativeAccepted(owner);
            VillageNeedsToolsIssueOwnership.SetOwner(owner, ownerControllerId);
        });
    }

    private void Handle_NetworkVillageCraftingIssueAcceptRejected(MessagePayload<NetworkVillageCraftingIssueAcceptRejected> payload)
    {
        var ownerId = payload.What.OwnerId;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(ownerId, out var owner)) return;
            issueInterface.RejectAcceptance(owner);
        });
    }
}
