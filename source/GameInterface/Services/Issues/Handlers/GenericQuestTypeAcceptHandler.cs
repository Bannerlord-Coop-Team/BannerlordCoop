using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using System;
using GameInterface.Services.Issues.Generic;
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
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Issues.Handlers;

internal class GenericQuestTypeAcceptHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<GenericQuestTypeAcceptHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly ITroopRosterInterface troopRosterInterface;
    private readonly IPrisonerSaleValidator troopValidator;
    private readonly IIssueOwnershipRegistry ownershipRegistry;

    public GenericQuestTypeAcceptHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IPlayerManager playerManager,
        ITroopRosterInterface troopRosterInterface,
        IPrisonerSaleValidator troopValidator,
        IIssueOwnershipRegistry ownershipRegistry)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;
        this.troopRosterInterface = troopRosterInterface;
        this.troopValidator = troopValidator;
        this.ownershipRegistry = ownershipRegistry;

        messageBroker.Subscribe<QuestTypeQuestSolutionAcceptTriggered>(Handle_QuestTypeQuestSolutionAcceptTriggered);
        messageBroker.Subscribe<RequestQuestTypeAcceptQuest>(Handle_RequestQuestTypeAcceptQuest);
        messageBroker.Subscribe<NetworkQuestTypeQuestAccepted>(Handle_NetworkQuestTypeQuestAccepted);

        messageBroker.Subscribe<QuestTypeAlternativeAcceptTriggered>(Handle_QuestTypeAlternativeAcceptTriggered);
        messageBroker.Subscribe<RequestQuestTypeAcceptAlternative>(Handle_RequestQuestTypeAcceptAlternative);
        messageBroker.Subscribe<NetworkQuestTypeAlternativeAccepted>(Handle_NetworkQuestTypeAlternativeAccepted);

        messageBroker.Subscribe<NetworkQuestTypeAcceptRejected>(Handle_NetworkQuestTypeAcceptRejected);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<QuestTypeQuestSolutionAcceptTriggered>(Handle_QuestTypeQuestSolutionAcceptTriggered);
        messageBroker.Unsubscribe<RequestQuestTypeAcceptQuest>(Handle_RequestQuestTypeAcceptQuest);
        messageBroker.Unsubscribe<NetworkQuestTypeQuestAccepted>(Handle_NetworkQuestTypeQuestAccepted);

        messageBroker.Unsubscribe<QuestTypeAlternativeAcceptTriggered>(Handle_QuestTypeAlternativeAcceptTriggered);
        messageBroker.Unsubscribe<RequestQuestTypeAcceptAlternative>(Handle_RequestQuestTypeAcceptAlternative);
        messageBroker.Unsubscribe<NetworkQuestTypeAlternativeAccepted>(Handle_NetworkQuestTypeAlternativeAccepted);

        messageBroker.Unsubscribe<NetworkQuestTypeAcceptRejected>(Handle_NetworkQuestTypeAcceptRejected);
    }

    private void Handle_QuestTypeQuestSolutionAcceptTriggered(MessagePayload<QuestTypeQuestSolutionAcceptTriggered> payload)
    {
        var owner = payload.What.Owner;
        if (owner == null || !objectManager.TryGetIdWithLogging(owner, out var ownerId)) return;

        if (ModInformation.IsServer)
        {
            var hostControllerId = payload.What.ControllerId;
            ownershipRegistry.SetOwner(owner, hostControllerId);
            network.SendAll(new NetworkQuestTypeQuestAccepted(ownerId, hostControllerId, payload.What.FieldsBytes));
        }
        else
        {
            IssueGenerationRegistry.TryGetGeneration(owner, out var generation);
            network.SendAll(new RequestQuestTypeAcceptQuest(ownerId, generation));
        }
    }

    private void Handle_RequestQuestTypeAcceptQuest(MessagePayload<RequestQuestTypeAcceptQuest> payload)
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
                    nameof(RequestQuestTypeAcceptQuest), ownerId);
                if (requester != null) network.Send(requester, new NetworkQuestTypeAcceptRejected(ownerId));
                return;
            }

            if (!IssueGenerationRegistry.TryGetGeneration(owner, out var currentGeneration) || currentGeneration != requestedGeneration)
            {
                Logger.Error("Rejecting {Message} for a stale/superseded issue generation for owner {Owner}",
                    nameof(RequestQuestTypeAcceptQuest), ownerId);
                network.Send(requester, new NetworkQuestTypeAcceptRejected(ownerId));
                return;
            }

            var descriptor = QuestTypeRegistry.Get(owner.Issue);
            if (descriptor?.TryArbitrateQuestSolutionAcceptBytes == null)
            {
                network.Send(requester, new NetworkQuestTypeAcceptRejected(ownerId));
                return;
            }

            var canAccept = owner.Issue.IsOngoingWithoutQuest && owner.Issue.IssueStayAliveConditions();
            var (accepted, fieldsBytes) = descriptor.TryArbitrateQuestSolutionAcceptBytes(owner, _ => canAccept);
            if (accepted)
            {
                ownershipRegistry.SetOwner(owner, player.ControllerId);
                network.SendAll(new NetworkQuestTypeQuestAccepted(ownerId, player.ControllerId, fieldsBytes));
            }
            else
            {
                if (canAccept)
                {
                    Logger.Error("Replayed accept for owner {Owner} but could not read back its quest fields - rolled back and rejecting", ownerId);
                }
                network.Send(requester, new NetworkQuestTypeAcceptRejected(ownerId));
            }
        });
    }

    private void Handle_NetworkQuestTypeQuestAccepted(MessagePayload<NetworkQuestTypeQuestAccepted> payload)
    {
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;

            try
            {
                QuestTypeRegistry.Get(owner.Issue)?.MirrorQuestSolutionAcceptBytes?.Invoke(owner, data.FieldsBytes);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to mirror {Message} for owner {Owner} - malformed or version-mismatched payload",
                    nameof(NetworkQuestTypeQuestAccepted), data.OwnerId);
                return;
            }

            ownershipRegistry.SetOwner(owner, data.OwnerControllerId);
        });
    }

    private void Handle_QuestTypeAlternativeAcceptTriggered(MessagePayload<QuestTypeAlternativeAcceptTriggered> payload)
    {
        var owner = payload.What.Owner;
        if (owner == null || !objectManager.TryGetIdWithLogging(owner, out var ownerId)) return;

        if (ModInformation.IsServer)
        {
            var hostControllerId = payload.What.ControllerId;
            ownershipRegistry.SetOwner(owner, hostControllerId);
            var hostTroops = troopRosterInterface.PackTroopRosterData(owner.Issue.AlternativeSolutionSentTroops);
            network.SendAll(new NetworkQuestTypeAlternativeAccepted(ownerId, hostControllerId, payload.What.FieldsBytes, hostTroops));
        }
        else
        {
            IssueGenerationRegistry.TryGetGeneration(owner, out var generation);
            var packedTroops = troopRosterInterface.PackTroopRosterData(owner.Issue.AlternativeSolutionSentTroops);
            network.SendAll(new RequestQuestTypeAcceptAlternative(ownerId, generation, packedTroops, payload.What.FieldsBytes));
        }
    }

    private void Handle_RequestQuestTypeAcceptAlternative(MessagePayload<RequestQuestTypeAcceptAlternative> payload)
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
                    nameof(RequestQuestTypeAcceptAlternative), ownerId);
                if (requester != null) network.Send(requester, new NetworkQuestTypeAcceptRejected(ownerId));
                return;
            }

            if (!IssueGenerationRegistry.TryGetGeneration(owner, out var currentGeneration) || currentGeneration != requestedGeneration)
            {
                Logger.Error("Rejecting {Message} for a stale/superseded issue generation for owner {Owner}",
                    nameof(RequestQuestTypeAcceptAlternative), ownerId);
                network.Send(requester, new NetworkQuestTypeAcceptRejected(ownerId));
                return;
            }

            var descriptor = QuestTypeRegistry.Get(owner.Issue);
            if (descriptor?.MirrorAlternativeAcceptBytes == null)
            {
                network.Send(requester, new NetworkQuestTypeAcceptRejected(ownerId));
                return;
            }

            if (owner.Issue.IsOngoingWithoutQuest && owner.Issue.IssueStayAliveConditions())
            {
                try
                {
                    ApplyValidatedSentTroops(owner, player, payload.What.SentTroops);
                    descriptor.MirrorAlternativeAcceptBytes(owner, payload.What.FieldsBytes);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Failed to apply {Message} for owner {Owner} - malformed or version-mismatched payload",
                        nameof(RequestQuestTypeAcceptAlternative), ownerId);
                    network.Send(requester, new NetworkQuestTypeAcceptRejected(ownerId));
                    return;
                }

                ownershipRegistry.SetOwner(owner, player.ControllerId);
                var validatedTroops = troopRosterInterface.PackTroopRosterData(owner.Issue.AlternativeSolutionSentTroops);
                network.SendAll(new NetworkQuestTypeAlternativeAccepted(ownerId, player.ControllerId, payload.What.FieldsBytes, validatedTroops));
            }
            else
            {
                network.Send(requester, new NetworkQuestTypeAcceptRejected(ownerId));
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

    private void Handle_NetworkQuestTypeAlternativeAccepted(MessagePayload<NetworkQuestTypeAlternativeAccepted> payload)
    {
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner) || owner.Issue == null) return;

            try
            {
                ApplyReceivedTroops(owner, data.SentTroops);
                QuestTypeRegistry.Get(owner.Issue)?.MirrorAlternativeAcceptBytes?.Invoke(owner, data.FieldsBytes);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to mirror {Message} for owner {Owner} - malformed or version-mismatched payload",
                    nameof(NetworkQuestTypeAlternativeAccepted), data.OwnerId);
                return;
            }

            ownershipRegistry.SetOwner(owner, data.OwnerControllerId);
        });
    }

    private void ApplyReceivedTroops(Hero owner, TroopRosterData troops)
    {
        using (new AllowedThread())
        {
            owner.Issue.AlternativeSolutionSentTroops.Clear();
            foreach (var element in troopRosterInterface.UnpackTroopRosterData(troops))
            {
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(
                    element.Character, element.Number, false, element.WoundedNumber, element.Xp, false);
            }
        }
    }

    private void Handle_NetworkQuestTypeAcceptRejected(MessagePayload<NetworkQuestTypeAcceptRejected> payload)
    {
        var ownerId = payload.What.OwnerId;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(ownerId, out var owner)) return;
            QuestTypeRegistry.Get(owner.Issue)?.RejectQuestSolutionAccept?.Invoke(owner);
        });
    }
}
