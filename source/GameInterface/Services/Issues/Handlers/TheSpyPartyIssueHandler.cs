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
using SandBox.Issues;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Issues.Handlers;

/// <summary>
/// Server-authoritative The Spy Party issue replication - creation (settlement pick) plus a bespoke
/// accept-time force-write of the selected-spy identity (see <see cref="ITheSpyPartyIssueInterface"/>'s type doc
/// comment for why this type needs both, unlike every other type in this batch). Same
/// genuine-accept-runs-locally-first, server-arbitrates-races, never-trust-a-client-claimed-ControllerId shape
/// as <see cref="VillageNeedsCraftingMaterialsIssueHandler"/>. Alternative-solution accept rides the fully
/// generic mirror (see <c>GenericAcceptMirrorIssueTypes.AlternativeSolutionMirrorEligible</c>) unchanged - no
/// handler code needed for it here. Removal rides the existing generic finalize choke point (see
/// <see cref="VillageNeedsCraftingMaterialsIssueHandler"/>'s doc comment for why per-type handlers deliberately
/// don't add their own parallel finalize subscription).
/// </summary>
internal class TheSpyPartyIssueHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<TheSpyPartyIssueHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ITheSpyPartyIssueInterface issueInterface;
    private readonly IPlayerManager playerManager;

    public TheSpyPartyIssueHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ITheSpyPartyIssueInterface issueInterface,
        IPlayerManager playerManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.issueInterface = issueInterface;
        this.playerManager = playerManager;

        messageBroker.Subscribe<TheSpyPartyIssueCreated>(Handle_TheSpyPartyIssueCreated);
        messageBroker.Subscribe<NetworkTheSpyPartyIssueCreated>(Handle_NetworkTheSpyPartyIssueCreated);

        messageBroker.Subscribe<TheSpyPartyIssueQuestAcceptTriggered>(Handle_TheSpyPartyIssueQuestAcceptTriggered);
        messageBroker.Subscribe<RequestTheSpyPartyIssueAcceptQuest>(Handle_RequestTheSpyPartyIssueAcceptQuest);
        messageBroker.Subscribe<NetworkTheSpyPartyIssueQuestAccepted>(Handle_NetworkTheSpyPartyIssueQuestAccepted);
        messageBroker.Subscribe<NetworkTheSpyPartyIssueAcceptRejected>(Handle_NetworkTheSpyPartyIssueAcceptRejected);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<TheSpyPartyIssueCreated>(Handle_TheSpyPartyIssueCreated);
        messageBroker.Unsubscribe<NetworkTheSpyPartyIssueCreated>(Handle_NetworkTheSpyPartyIssueCreated);

        messageBroker.Unsubscribe<TheSpyPartyIssueQuestAcceptTriggered>(Handle_TheSpyPartyIssueQuestAcceptTriggered);
        messageBroker.Unsubscribe<RequestTheSpyPartyIssueAcceptQuest>(Handle_RequestTheSpyPartyIssueAcceptQuest);
        messageBroker.Unsubscribe<NetworkTheSpyPartyIssueQuestAccepted>(Handle_NetworkTheSpyPartyIssueQuestAccepted);
        messageBroker.Unsubscribe<NetworkTheSpyPartyIssueAcceptRejected>(Handle_NetworkTheSpyPartyIssueAcceptRejected);
    }

    // --- Creation: server rolls once, broadcasts the resolved tournament settlement ---

    private void Handle_TheSpyPartyIssueCreated(MessagePayload<TheSpyPartyIssueCreated> payload)
    {
        if (ModInformation.IsClient) return;

        var issue = payload.What.Issue;
        if (issue?.IssueOwner == null) return;
        if (!objectManager.TryGetIdWithLogging(issue.IssueOwner, out var ownerId)) return;

        if (!issueInterface.TryCaptureFields(issue, out var selectedSettlement))
        {
            Logger.Error("Could not capture The Spy Party issue fields for owner {Owner}", ownerId);
            return;
        }

        if (!objectManager.TryGetIdWithLogging(selectedSettlement, out var selectedSettlementId)) return;

        network.SendAll(new NetworkTheSpyPartyIssueCreated(ownerId, selectedSettlementId));
    }

    private void Handle_NetworkTheSpyPartyIssueCreated(MessagePayload<NetworkTheSpyPartyIssueCreated> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;
            if (owner.Issue != null) return; // idempotent

            if (!objectManager.TryGetObjectWithLogging<Settlement>(data.SelectedSettlementId, out var selectedSettlement)) return;

            var replicated = issueInterface.ConstructReplicated(owner, selectedSettlement);

            issueInterface.RegisterReplicated(owner, replicated);
        });
    }

    // --- Acceptance: the accepting machine already applied it locally for real (with its own, not-yet-
    // authoritative spy roll); the server arbitrates a same-issue double-accept race and resolves/broadcasts
    // the authoritative spy identity every peer force-corrects to. ---

    private void Handle_TheSpyPartyIssueQuestAcceptTriggered(MessagePayload<TheSpyPartyIssueQuestAcceptTriggered> payload)
    {
        var owner = payload.What.Owner;
        if (owner == null || !objectManager.TryGetIdWithLogging(owner, out var ownerId)) return;

        if (ModInformation.IsServer)
        {
            // The host's own live conversation just accepted - its own roll IS authoritative. Read back what it
            // baked and broadcast it.
            if (!issueInterface.TryCaptureSelectedSpyIndex(owner, out var selectedSpyIndex))
            {
                Logger.Error("Host accepted The Spy Party quest for owner {Owner} but could not read back the selected spy index", ownerId);
                return;
            }

            var hostControllerId = payload.What.ControllerId;
            VillageNeedsToolsIssueOwnership.SetOwner(owner, hostControllerId);
            network.SendAll(new NetworkTheSpyPartyIssueQuestAccepted(ownerId, hostControllerId, selectedSpyIndex));
        }
        else
        {
            // A client's own live conversation already applied this locally with its own (not yet
            // authoritative) spy roll - tell the server so it can arbitrate a same-issue double-accept race and
            // resolve the real spy identity itself.
            network.SendAll(new RequestTheSpyPartyIssueAcceptQuest(ownerId));
        }
    }

    private void Handle_RequestTheSpyPartyIssueAcceptQuest(MessagePayload<RequestTheSpyPartyIssueAcceptQuest> payload)
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
                    nameof(RequestTheSpyPartyIssueAcceptQuest), ownerId);
                if (requester != null) network.Send(requester, new NetworkTheSpyPartyIssueAcceptRejected(ownerId));
                return;
            }

            if (owner.Issue is TheSpyPartyIssueQuestBehavior.TheSpyPartyIssue && owner.Issue.IsOngoingWithoutQuest)
            {
                // First valid request wins the race - replay on the server's own authoritative copy, capture
                // what that replay actually rolled (the server's own selection IS the authoritative one), then
                // confirm it to everyone.
                issueInterface.ReplayQuestAccepted(owner);
                if (!issueInterface.TryCaptureSelectedSpyIndex(owner, out var selectedSpyIndex))
                {
                    Logger.Error("Replayed StartIssueQuest for owner {Owner} but could not read back the selected spy index", ownerId);
                    return;
                }

                VillageNeedsToolsIssueOwnership.SetOwner(owner, player.ControllerId);
                network.SendAll(new NetworkTheSpyPartyIssueQuestAccepted(ownerId, player.ControllerId, selectedSpyIndex));
            }
            else
            {
                network.Send(requester, new NetworkTheSpyPartyIssueAcceptRejected(ownerId));
            }
        });
    }

    private void Handle_NetworkTheSpyPartyIssueQuestAccepted(MessagePayload<NetworkTheSpyPartyIssueQuestAccepted> payload)
    {
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;
            // Mirror (replaying if this peer has no quest yet) and force-correct the selected-spy index in the
            // same synchronous block that also records ownership - no TOCTOU window, same shape as every other
            // handler's equivalent.
            issueInterface.MirrorQuestAccepted(owner, data.SelectedSpyIndex);
            VillageNeedsToolsIssueOwnership.SetOwner(owner, data.OwnerControllerId);
        });
    }

    private void Handle_NetworkTheSpyPartyIssueAcceptRejected(MessagePayload<NetworkTheSpyPartyIssueAcceptRejected> payload)
    {
        var ownerId = payload.What.OwnerId;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(ownerId, out var owner)) return;
            issueInterface.RejectAcceptance(owner);
        });
    }
}
