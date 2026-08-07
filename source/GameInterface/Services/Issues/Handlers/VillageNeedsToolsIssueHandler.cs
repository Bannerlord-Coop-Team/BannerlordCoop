using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.Migrated.VillageNeedsTools;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Party;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.TroopRosters.Data;
using GameInterface.Services.TroopRosters.Interfaces;
using LiteNetLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Handlers;

internal class VillageNeedsToolsIssueHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<VillageNeedsToolsIssueHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IVillageNeedsToolsIssueInterface issueInterface;
    private readonly IPlayerManager playerManager;
    private readonly ITroopRosterInterface troopRosterInterface;
    private readonly IPrisonerSaleValidator troopValidator;

    public VillageNeedsToolsIssueHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IVillageNeedsToolsIssueInterface issueInterface,
        IPlayerManager playerManager,
        ITroopRosterInterface troopRosterInterface,
        IPrisonerSaleValidator troopValidator)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.issueInterface = issueInterface;
        this.playerManager = playerManager;
        this.troopRosterInterface = troopRosterInterface;
        this.troopValidator = troopValidator;

        messageBroker.Subscribe<VillageIssueCreated>(Handle_VillageIssueCreated);
        messageBroker.Subscribe<NetworkVillageIssueCreated>(Handle_NetworkVillageIssueCreated);

        messageBroker.Subscribe<VillageIssueQuestAcceptTriggered>(Handle_VillageIssueQuestAcceptTriggered);
        messageBroker.Subscribe<RequestVillageIssueAcceptQuest>(Handle_RequestVillageIssueAcceptQuest);
        messageBroker.Subscribe<NetworkVillageIssueQuestAccepted>(Handle_NetworkVillageIssueQuestAccepted);

        messageBroker.Subscribe<VillageIssueAlternativeAcceptTriggered>(Handle_VillageIssueAlternativeAcceptTriggered);
        messageBroker.Subscribe<RequestVillageIssueAcceptAlternative>(Handle_RequestVillageIssueAcceptAlternative);
        messageBroker.Subscribe<NetworkVillageIssueAlternativeAccepted>(Handle_NetworkVillageIssueAlternativeAccepted);

        messageBroker.Subscribe<NetworkVillageIssueAcceptRejected>(Handle_NetworkVillageIssueAcceptRejected);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<VillageIssueCreated>(Handle_VillageIssueCreated);
        messageBroker.Unsubscribe<NetworkVillageIssueCreated>(Handle_NetworkVillageIssueCreated);

        messageBroker.Unsubscribe<VillageIssueQuestAcceptTriggered>(Handle_VillageIssueQuestAcceptTriggered);
        messageBroker.Unsubscribe<RequestVillageIssueAcceptQuest>(Handle_RequestVillageIssueAcceptQuest);
        messageBroker.Unsubscribe<NetworkVillageIssueQuestAccepted>(Handle_NetworkVillageIssueQuestAccepted);

        messageBroker.Unsubscribe<VillageIssueAlternativeAcceptTriggered>(Handle_VillageIssueAlternativeAcceptTriggered);
        messageBroker.Unsubscribe<RequestVillageIssueAcceptAlternative>(Handle_RequestVillageIssueAcceptAlternative);
        messageBroker.Unsubscribe<NetworkVillageIssueAlternativeAccepted>(Handle_NetworkVillageIssueAlternativeAccepted);

        messageBroker.Unsubscribe<NetworkVillageIssueAcceptRejected>(Handle_NetworkVillageIssueAcceptRejected);
    }

    private void Handle_VillageIssueCreated(MessagePayload<VillageIssueCreated> payload)
    {
        if (ModInformation.IsClient) return;

        var issue = payload.What.Issue;
        if (issue?.IssueOwner == null) return;
        if (!objectManager.TryGetIdWithLogging(issue.IssueOwner, out var ownerId)) return;

        if (!VillageNeedsToolsQuestType.CreationCapture.TryCapture(issue, out var fields))
        {
            Logger.Error("Could not capture Village Needs Tools issue fields for owner {Owner}", ownerId);
            return;
        }
        var (requestedItem, exchangeItem, numberOfExchangeItem, numberOfRequestedItem, payment) = fields;

        if (!objectManager.TryGetIdWithLogging(requestedItem, out var requestedItemId)) return;

        string exchangeItemId = null;
        if (exchangeItem != null && !objectManager.TryGetIdWithLogging(exchangeItem, out exchangeItemId)) return;

        var generation = IssueGenerationRegistry.Bump(issue.IssueOwner);

        network.SendAll(new NetworkVillageIssueCreated(
            ownerId, requestedItemId, exchangeItemId, numberOfRequestedItem, numberOfExchangeItem, payment, generation));
    }

    private void Handle_NetworkVillageIssueCreated(MessagePayload<NetworkVillageIssueCreated> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;

            IssueGenerationRegistry.SetGeneration(owner, data.Generation);

            if (owner.Issue != null) return;

            if (!objectManager.TryGetObjectWithLogging<ItemObject>(data.RequestedItemId, out var requestedItem)) return;

            ItemObject exchangeItem = null;
            if (data.ExchangeItemId != null &&
                !objectManager.TryGetObjectWithLogging<ItemObject>(data.ExchangeItemId, out exchangeItem)) return;

            VillageNeedsToolsQuestType.CreationCapture.ConstructAndRegisterReplicated(
                owner, (requestedItem, exchangeItem, data.NumberOfExchangeItem, data.NumberOfRequestedItem, data.Payment));
        });
    }

    private void Handle_VillageIssueQuestAcceptTriggered(MessagePayload<VillageIssueQuestAcceptTriggered> payload)
    {
        var owner = payload.What.Owner;
        if (owner == null || !objectManager.TryGetIdWithLogging(owner, out var ownerId)) return;

        if (ModInformation.IsServer)
        {
            var hostControllerId = payload.What.ControllerId;
            IssueOwnershipRegistry.SetOwner(owner, hostControllerId);
            network.SendAll(new NetworkVillageIssueQuestAccepted(ownerId, hostControllerId));
        }
        else
        {
            IssueGenerationRegistry.TryGetGeneration(owner, out var generation);
            network.SendAll(new RequestVillageIssueAcceptQuest(ownerId, generation));
        }
    }

    private void Handle_RequestVillageIssueAcceptQuest(MessagePayload<RequestVillageIssueAcceptQuest> payload)
    {
        if (ModInformation.IsClient) return;

        var ownerId = payload.What.OwnerId;
        var requestedGeneration = payload.What.Generation;
        var requester = payload.Who as NetPeer;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(ownerId, out var owner)) return;

            // Identity is derived server-side from the authenticated NetPeer, never trusted from a
            // client-claimed value - a client cannot claim a different ControllerId to steal ownership.
            if (requester == null || !playerManager.TryGetPlayer(requester, out var player))
            {
                Logger.Error("Rejecting {Message} from an unregistered/unknown requester for owner {Owner}",
                    nameof(RequestVillageIssueAcceptQuest), ownerId);
                if (requester != null) network.Send(requester, new NetworkVillageIssueAcceptRejected(ownerId));
                return;
            }

            if (!IssueGenerationRegistry.TryGetGeneration(owner, out var currentGeneration) || currentGeneration != requestedGeneration)
            {
                Logger.Error("Rejecting {Message} for a stale/superseded issue generation for owner {Owner}",
                    nameof(RequestVillageIssueAcceptQuest), ownerId);
                network.Send(requester, new NetworkVillageIssueAcceptRejected(ownerId));
                return;
            }

            if (GenericAcceptMirrorIssueTypes.IsQuestSolutionMirrorEligible(owner.Issue) && owner.Issue.IsOngoingWithoutQuest &&
                owner.Issue.IssueStayAliveConditions())
            {
                issueInterface.EnsureServerQuestMirror(owner);
                IssueOwnershipRegistry.SetOwner(owner, player.ControllerId);
                network.SendAll(new NetworkVillageIssueQuestAccepted(ownerId, player.ControllerId));
            }
            else
            {
                network.Send(requester, new NetworkVillageIssueAcceptRejected(ownerId));
            }
        });
    }

    private void Handle_NetworkVillageIssueQuestAccepted(MessagePayload<NetworkVillageIssueQuestAccepted> payload)
    {
        var ownerId = payload.What.OwnerId;
        var ownerControllerId = payload.What.OwnerControllerId;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(ownerId, out var owner)) return;
            // Mirror and ownership record happen in the same synchronous block - no TOCTOU window between
            // "dialogue registered" and "ownership known".
            issueInterface.MirrorQuestAccepted(owner);
            IssueOwnershipRegistry.SetOwner(owner, ownerControllerId);
        });
    }

    private void Handle_VillageIssueAlternativeAcceptTriggered(MessagePayload<VillageIssueAlternativeAcceptTriggered> payload)
    {
        var owner = payload.What.Owner;
        if (owner == null || !objectManager.TryGetIdWithLogging(owner, out var ownerId)) return;

        if (ModInformation.IsServer)
        {
            var hostControllerId = payload.What.ControllerId;
            IssueOwnershipRegistry.SetOwner(owner, hostControllerId);
            network.SendAll(new NetworkVillageIssueAlternativeAccepted(ownerId, hostControllerId));
        }
        else
        {
            IssueGenerationRegistry.TryGetGeneration(owner, out var generation);
            var packedTroops = troopRosterInterface.PackTroopRosterData(owner.Issue.AlternativeSolutionSentTroops);
            network.SendAll(new RequestVillageIssueAcceptAlternative(ownerId, generation, packedTroops));
        }
    }

    private void Handle_RequestVillageIssueAcceptAlternative(MessagePayload<RequestVillageIssueAcceptAlternative> payload)
    {
        if (ModInformation.IsClient) return;

        var ownerId = payload.What.OwnerId;
        var requestedGeneration = payload.What.Generation;
        var requester = payload.Who as NetPeer;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(ownerId, out var owner)) return;

            if (requester == null || !playerManager.TryGetPlayer(requester, out var player))
            {
                Logger.Error("Rejecting {Message} from an unregistered/unknown requester for owner {Owner}",
                    nameof(RequestVillageIssueAcceptAlternative), ownerId);
                if (requester != null) network.Send(requester, new NetworkVillageIssueAcceptRejected(ownerId));
                return;
            }

            if (!IssueGenerationRegistry.TryGetGeneration(owner, out var currentGeneration) || currentGeneration != requestedGeneration)
            {
                Logger.Error("Rejecting {Message} for a stale/superseded issue generation for owner {Owner}",
                    nameof(RequestVillageIssueAcceptAlternative), ownerId);
                network.Send(requester, new NetworkVillageIssueAcceptRejected(ownerId));
                return;
            }

            if (GenericAcceptMirrorIssueTypes.IsAlternativeSolutionMirrorEligible(owner.Issue) && owner.Issue.IsOngoingWithoutQuest &&
                owner.Issue.IssueStayAliveConditions())
            {
                ApplyValidatedSentTroops(owner, player, payload.What.SentTroops);
                issueInterface.MirrorAlternativeAccepted(owner);
                IssueOwnershipRegistry.SetOwner(owner, player.ControllerId);
                network.SendAll(new NetworkVillageIssueAlternativeAccepted(ownerId, player.ControllerId));
            }
            else
            {
                network.Send(requester, new NetworkVillageIssueAcceptRejected(ownerId));
            }
        });
    }

    private void ApplyValidatedSentTroops(Hero owner, Player player, TroopRosterData claimedTroops)
    {
        var claimedRoster = TroopRoster.CreateDummyTroopRoster();
        foreach (var element in troopRosterInterface.UnpackTroopRosterData(claimedTroops))
        {
            claimedRoster.AddToCounts(element.Character, element.Number, false, element.WoundedNumber, element.Xp, false);
        }

        var validatedRoster = player.MobilePartyId != null &&
            objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party)
            ? troopValidator.Validate(claimedRoster, party.MemberRoster)
            : TroopRoster.CreateDummyTroopRoster();

        using (new AllowedThread())
        {
            owner.Issue.AlternativeSolutionSentTroops.Clear();
            foreach (var element in validatedRoster.GetTroopRoster())
            {
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(
                    element.Character, element.Number, false, element.WoundedNumber, element.Xp, false);
            }
        }
    }

    private void Handle_NetworkVillageIssueAlternativeAccepted(MessagePayload<NetworkVillageIssueAlternativeAccepted> payload)
    {
        var ownerId = payload.What.OwnerId;
        var ownerControllerId = payload.What.OwnerControllerId;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(ownerId, out var owner)) return;
            issueInterface.MirrorAlternativeAccepted(owner);
            IssueOwnershipRegistry.SetOwner(owner, ownerControllerId);
        });
    }

    private void Handle_NetworkVillageIssueAcceptRejected(MessagePayload<NetworkVillageIssueAcceptRejected> payload)
    {
        var ownerId = payload.What.OwnerId;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(ownerId, out var owner)) return;
            issueInterface.RejectAcceptance(owner);
        });
    }
}
