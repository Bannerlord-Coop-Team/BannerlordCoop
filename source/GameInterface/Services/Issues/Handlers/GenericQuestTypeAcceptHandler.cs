using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using System;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.AcceptMirror;
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
    private readonly IIssueGenerationRegistry generationRegistry;
    private readonly IIssueConversationTracker conversationTracker;

    public GenericQuestTypeAcceptHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IPlayerManager playerManager,
        ITroopRosterInterface troopRosterInterface,
        IPrisonerSaleValidator troopValidator,
        IIssueOwnershipRegistry ownershipRegistry,
        IIssueGenerationRegistry generationRegistry,
        IIssueConversationTracker conversationTracker)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;
        this.troopRosterInterface = troopRosterInterface;
        this.troopValidator = troopValidator;
        this.ownershipRegistry = ownershipRegistry;
        this.generationRegistry = generationRegistry;
        this.conversationTracker = conversationTracker;

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

        var descriptor = QuestTypeRegistry.Get(owner.Issue);
        if (descriptor?.SupportsQuestSolutionAccept != true) return;

        if (ModInformation.IsServer)
        {
            var hostControllerId = payload.What.ControllerId;
            if (hostControllerId == null || !playerManager.TryGetPlayer(hostControllerId, out var player)) return;

            try
            {
                byte[] fieldsBytes = null;
                var started = QuestSolutionStartRunner.RunGuarded(player, () =>
                {
                    if (descriptor.TryArbitrateQuestSolutionAcceptBytes != null)
                    {
                        var (accepted, bytes) = descriptor.TryArbitrateQuestSolutionAcceptBytes(owner, _ => true);
                        fieldsBytes = bytes;
                        return accepted;
                    }
                    return owner.Issue.StartIssueWithQuest();
                });
                if (!started) return;

                ownershipRegistry.SetOwner(owner, hostControllerId);
                network.SendAll(new NetworkQuestTypeQuestAccepted(ownerId, hostControllerId, fieldsBytes));
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to start the host's own quest-solution accept for owner {Owner} - not broadcasting", ownerId);
            }
        }
        else
        {
            generationRegistry.TryGetGeneration(owner, out var generation);
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
                if (requester != null) network.Send(requester, new NetworkQuestTypeAcceptRejected(ownerId, isAlternative: false));
                return;
            }

            if (!generationRegistry.TryGetGeneration(owner, out var currentGeneration) || currentGeneration != requestedGeneration)
            {
                Logger.Error("Rejecting {Message} for a stale/superseded issue generation for owner {Owner}",
                    nameof(RequestQuestTypeAcceptQuest), ownerId);
                network.Send(requester, new NetworkQuestTypeAcceptRejected(ownerId, isAlternative: false));
                return;
            }

            if (!conversationTracker.TryGetTrackedRequester(ownerId, player.ControllerId, out var trackedGeneration) ||
                trackedGeneration != requestedGeneration)
            {
                Logger.Error("Rejecting {Message} for a requester with no tracked conversation with owner {Owner}",
                    nameof(RequestQuestTypeAcceptQuest), ownerId);
                network.Send(requester, new NetworkQuestTypeAcceptRejected(ownerId, isAlternative: false));
                return;
            }

            var descriptor = QuestTypeRegistry.Get(owner.Issue);
            var canAccept = descriptor?.SupportsQuestSolutionAccept == true &&
                owner.Issue.IsOngoingWithoutQuest && owner.Issue.IssueStayAliveConditions();
            if (!canAccept)
            {
                network.Send(requester, new NetworkQuestTypeAcceptRejected(ownerId, isAlternative: false));
                return;
            }

            byte[] fieldsBytes = null;
            try
            {
                var started = QuestSolutionStartRunner.RunGuarded(player, () =>
                {
                    if (descriptor.TryArbitrateQuestSolutionAcceptBytes != null)
                    {
                        var (accepted, bytes) = descriptor.TryArbitrateQuestSolutionAcceptBytes(owner, _ => canAccept);
                        fieldsBytes = bytes;
                        return accepted;
                    }
                    return owner.Issue.StartIssueWithQuest();
                });
                if (!started)
                {
                    Logger.Error("Replayed accept for owner {Owner} but could not read back its quest fields - rolled back and rejecting", ownerId);
                    network.Send(requester, new NetworkQuestTypeAcceptRejected(ownerId, isAlternative: false));
                    return;
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to apply {Message} for owner {Owner} - not broadcasting",
                    nameof(RequestQuestTypeAcceptQuest), ownerId);
                network.Send(requester, new NetworkQuestTypeAcceptRejected(ownerId, isAlternative: false));
                return;
            }

            ownershipRegistry.SetOwner(owner, player.ControllerId);
            network.SendAll(new NetworkQuestTypeQuestAccepted(ownerId, player.ControllerId, fieldsBytes));
        });
    }

    private void Handle_NetworkQuestTypeQuestAccepted(MessagePayload<NetworkQuestTypeQuestAccepted> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;

            var descriptor = QuestTypeRegistry.Get(owner.Issue);
            try
            {
                if (descriptor?.MirrorQuestSolutionAcceptBytes != null)
                {
                    descriptor.MirrorQuestSolutionAcceptBytes(owner, data.FieldsBytes);
                }
                else
                {
                    MirrorQuestAccepted(owner);
                }
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

        var descriptor = QuestTypeRegistry.Get(owner.Issue);
        if (descriptor?.SupportsAlternativeAccept != true) return;

        if (ModInformation.IsServer)
        {
            var hostControllerId = payload.What.ControllerId;
            if (hostControllerId == null || !playerManager.TryGetPlayer(hostControllerId, out var player)) return;

            AlternativeSolutionVanillaState state;
            try
            {
                state = AlternativeSolutionStartRunner.StartOnServer(owner, player);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to start the host's own alternative-solution accept for owner {Owner} - rolling back", ownerId);
                RollbackFailedAlternativeAcceptStart(owner, hostControllerId);
                return;
            }

            byte[] fieldsBytes = null;
            if (descriptor.TryArbitrateAlternativeAcceptBytes != null)
            {
                var (accepted, bytes) = descriptor.TryArbitrateAlternativeAcceptBytes(owner, _ => true);
                if (!accepted)
                {
                    RollbackFailedAlternativeAcceptStart(owner, hostControllerId);
                    return;
                }
                fieldsBytes = bytes;
            }

            ownershipRegistry.SetOwner(owner, hostControllerId);
            var hostTroops = troopRosterInterface.PackTroopRosterData(owner.Issue.AlternativeSolutionSentTroops);
            network.SendAll(new NetworkQuestTypeAlternativeAccepted(ownerId, hostControllerId, state, fieldsBytes, hostTroops));
        }
        else
        {
            generationRegistry.TryGetGeneration(owner, out var generation);
            var packedTroops = troopRosterInterface.PackTroopRosterData(owner.Issue.AlternativeSolutionSentTroops);
            network.SendAll(new RequestQuestTypeAcceptAlternative(ownerId, generation, packedTroops));
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
                if (requester != null) network.Send(requester, new NetworkQuestTypeAcceptRejected(ownerId, isAlternative: true));
                return;
            }

            if (!generationRegistry.TryGetGeneration(owner, out var currentGeneration) || currentGeneration != requestedGeneration)
            {
                Logger.Error("Rejecting {Message} for a stale/superseded issue generation for owner {Owner}",
                    nameof(RequestQuestTypeAcceptAlternative), ownerId);
                network.Send(requester, new NetworkQuestTypeAcceptRejected(ownerId, isAlternative: true));
                return;
            }

            if (!conversationTracker.TryGetTrackedRequester(ownerId, player.ControllerId, out var trackedGeneration) ||
                trackedGeneration != requestedGeneration)
            {
                Logger.Error("Rejecting {Message} for a requester with no tracked conversation with owner {Owner}",
                    nameof(RequestQuestTypeAcceptAlternative), ownerId);
                network.Send(requester, new NetworkQuestTypeAcceptRejected(ownerId, isAlternative: true));
                return;
            }

            var descriptor = QuestTypeRegistry.Get(owner.Issue);
            var canAccept = descriptor?.SupportsAlternativeAccept == true &&
                owner.Issue.IsOngoingWithoutQuest && owner.Issue.IssueStayAliveConditions();
            if (!canAccept)
            {
                network.Send(requester, new NetworkQuestTypeAcceptRejected(ownerId, isAlternative: true));
                return;
            }

            TroopRoster validatedRoster;
            try
            {
                validatedRoster = BuildValidatedSentTroops(player, payload.What.SentTroops);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to apply {Message} for owner {Owner} - malformed or version-mismatched payload",
                    nameof(RequestQuestTypeAcceptAlternative), ownerId);
                network.Send(requester, new NetworkQuestTypeAcceptRejected(ownerId, isAlternative: true));
                return;
            }

            if (validatedRoster.TotalHeroes == 0)
            {
                Logger.Error("Rejecting {Message} for owner {Owner} - requester's validated troop roster is empty",
                    nameof(RequestQuestTypeAcceptAlternative), ownerId);
                network.Send(requester, new NetworkQuestTypeAcceptRejected(ownerId, isAlternative: true));
                return;
            }

            AlternativeSolutionVanillaState state;
            byte[] fieldsBytes = null;
            try
            {
                state = AlternativeSolutionStartRunner.StartOnServerFromClaim(owner, player, validatedRoster);

                if (descriptor.TryArbitrateAlternativeAcceptBytes != null)
                {
                    var (accepted, bytes) = descriptor.TryArbitrateAlternativeAcceptBytes(owner, _ => true);
                    if (!accepted)
                    {
                        RollbackFailedAlternativeAcceptStart(owner, player.ControllerId);
                        network.Send(requester, new NetworkQuestTypeAcceptRejected(ownerId, isAlternative: true));
                        return;
                    }
                    fieldsBytes = bytes;
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to start {Message} for owner {Owner} after troop validation - rolling back",
                    nameof(RequestQuestTypeAcceptAlternative), ownerId);
                RollbackFailedAlternativeAcceptStart(owner, player.ControllerId);
                network.Send(requester, new NetworkQuestTypeAcceptRejected(ownerId, isAlternative: true));
                return;
            }

            ownershipRegistry.SetOwner(owner, player.ControllerId);
            var validatedTroops = troopRosterInterface.PackTroopRosterData(owner.Issue.AlternativeSolutionSentTroops);
            network.SendAll(new NetworkQuestTypeAlternativeAccepted(ownerId, player.ControllerId, state, fieldsBytes, validatedTroops));
        });
    }

    private TroopRoster BuildValidatedSentTroops(Player player, TroopRosterData claimedTroops)
    {
        var claimedRoster = TroopRoster.CreateDummyTroopRoster();
        foreach (var element in troopRosterInterface.UnpackTroopRosterData(claimedTroops))
        {
            claimedRoster.AddToCounts(element.Character, element.Number, false, element.WoundedNumber, element.Xp, false);
        }

        return player.MobilePartyId != null &&
            objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party)
            ? troopValidator.Validate(claimedRoster, party.MemberRoster, preserveTroopXp: true)
            : TroopRoster.CreateDummyTroopRoster();
    }

    private void Handle_NetworkQuestTypeAlternativeAccepted(MessagePayload<NetworkQuestTypeAlternativeAccepted> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner) || owner.Issue == null) return;

            var descriptor = QuestTypeRegistry.Get(owner.Issue);
            try
            {
                ApplyReceivedTroops(owner, data.SentTroops);
                MirrorAlternativeAccepted(owner, data.State);
                descriptor?.MirrorAlternativeAcceptBytes?.Invoke(owner, data.FieldsBytes);
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

    private static void RollbackAlternativeAccept(Hero owner)
    {
        if (owner?.Issue == null) return;

        using (new AllowedThread())
        {
            var sentTroops = owner.Issue.AlternativeSolutionSentTroops;
            if (MobileParty.MainParty != null && sentTroops.TotalManCount > 0)
            {
                MobileParty.MainParty.MemberRoster.Add(sentTroops);
            }
            sentTroops.Clear();
        }
    }

    private void RollbackFailedAlternativeAcceptStart(Hero owner, string controllerId)
    {
        if (owner?.Issue == null) return;

        Hero trueOwnerHero = null;
        MobileParty ownerParty = null;
        if (!string.IsNullOrEmpty(controllerId) && playerManager.TryGetPlayer(controllerId, out var player))
        {
            if (player.HeroId != null) objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out trueOwnerHero);
            if (player.MobilePartyId != null) objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out ownerParty);
        }

        using (new MainHeroSubstitutionScope(trueOwnerHero ?? owner, ownerParty))
        using (new AllowedThread())
        {
            var issue = owner.Issue;
            var sentTroops = issue.AlternativeSolutionSentTroops;
            if (MobileParty.MainParty != null && sentTroops.TotalManCount > 0)
            {
                MobileParty.MainParty.MemberRoster.Add(sentTroops);
            }
            sentTroops.Clear();
            issue._issueState = IssueBase.IssueState.Ongoing;
        }

        ownershipRegistry.Clear(owner);
    }

    private void Handle_NetworkQuestTypeAcceptRejected(MessagePayload<NetworkQuestTypeAcceptRejected> payload)
    {
        if (ModInformation.IsServer) return;

        var ownerId = payload.What.OwnerId;
        var isAlternative = payload.What.IsAlternative;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(ownerId, out var owner)) return;

            var descriptor = QuestTypeRegistry.Get(owner.Issue);
            if (isAlternative)
            {
                if (descriptor?.RejectAlternativeAccept != null)
                {
                    descriptor.RejectAlternativeAccept(owner);
                }
                else
                {
                    RollbackAlternativeAccept(owner);
                }
            }
            else if (descriptor?.RejectQuestSolutionAccept != null)
            {
                descriptor.RejectQuestSolutionAccept(owner);
            }
            else
            {
                AcceptMirrorSupport.RejectAcceptance(owner);
            }
        });
    }

    private static void MirrorQuestAccepted(Hero owner)
    {
        if (owner?.Issue == null || !owner.Issue.IsOngoingWithoutQuest) return;

        var issue = owner.Issue;
        using (new AllowedThread())
        {
            issue._issueState = IssueBase.IssueState.SolvingWithQuestSolution;
            issue.IsTriedToSolveBefore = true;
            issue.IssueDueTime = CampaignTime.Never;
        }
    }

    private static void MirrorAlternativeAccepted(Hero owner, AlternativeSolutionVanillaState state)
    {
        if (owner?.Issue == null || !owner.Issue.IsOngoingWithoutQuest) return;

        var issue = owner.Issue;
        using (new AllowedThread())
        {
            issue._issueState = IssueBase.IssueState.SolvingWithAlternativeSolution;
            issue.IsTriedToSolveBefore = true;
            AlternativeSolutionVanillaStateSync.Apply(issue, state);
        }
    }
}
