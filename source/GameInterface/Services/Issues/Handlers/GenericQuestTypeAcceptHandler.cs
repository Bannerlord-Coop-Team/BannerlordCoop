using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using System;
using System.Collections.Generic;
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
    private readonly Dictionary<Hero, (IssueBase Issue, MobileParty Party, TroopRoster Troops, TroopRoster Roster, PartyScreenLogic Screen, bool ResetObserved)> pendingAlternativeAccepts = new();
    private readonly Dictionary<Hero, (IssueBase Issue, TroopRosterData Troops)> acceptedAlternativeTroops = new();
    private readonly Dictionary<TroopRoster, (TroopRoster Before, TroopRoster After)> localSelectionTransfers = new();
    private readonly Dictionary<TroopRoster, (PartyScreenLogic Screen, TroopRoster SelectedTroops)> resetQuestScreens = new();

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
        messageBroker.Subscribe<QuestAlternativeTroopsTransferredLocally>(Handle_QuestAlternativeTroopsTransferredLocally);
        messageBroker.Subscribe<QuestAlternativeTroopSelectionClosed>(Handle_QuestAlternativeTroopSelectionClosed);
        messageBroker.Subscribe<QuestAlternativeTroopSelectionReset>(Handle_QuestAlternativeTroopSelectionReset);
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
        messageBroker.Unsubscribe<QuestAlternativeTroopsTransferredLocally>(Handle_QuestAlternativeTroopsTransferredLocally);
        messageBroker.Unsubscribe<QuestAlternativeTroopSelectionClosed>(Handle_QuestAlternativeTroopSelectionClosed);
        messageBroker.Unsubscribe<QuestAlternativeTroopSelectionReset>(Handle_QuestAlternativeTroopSelectionReset);
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

    private bool TryValidateAcceptRequest(
        NetPeer requester, string ownerId, Hero owner, int requestedGeneration, bool isAlternative, string messageName,
        out Player player, out QuestTypeDescriptor descriptor)
    {
        player = null;
        if (requester == null || !playerManager.TryGetPlayer(requester, out player))
        {
            Logger.Error("Rejecting {Message} from an unregistered/unknown requester for owner {Owner}",
                messageName, ownerId);
            if (requester != null) network.Send(requester, new NetworkQuestTypeAcceptRejected(ownerId, isAlternative));
            descriptor = null;
            return false;
        }

        if (!generationRegistry.TryGetGeneration(owner, out var currentGeneration) || currentGeneration != requestedGeneration)
        {
            Logger.Error("Rejecting {Message} for a stale/superseded issue generation for owner {Owner}",
                messageName, ownerId);
            network.Send(requester, new NetworkQuestTypeAcceptRejected(ownerId, isAlternative));
            descriptor = null;
            return false;
        }

        if (!conversationTracker.TryGetTrackedRequester(ownerId, player.ControllerId, out var trackedGeneration) ||
            trackedGeneration != requestedGeneration)
        {
            Logger.Error("Rejecting {Message} for a requester with no tracked conversation with owner {Owner}",
                messageName, ownerId);
            network.Send(requester, new NetworkQuestTypeAcceptRejected(ownerId, isAlternative));
            descriptor = null;
            return false;
        }

        descriptor = QuestTypeRegistry.Get(owner.Issue);
        var supports = isAlternative ? descriptor?.SupportsAlternativeAccept == true : descriptor?.SupportsQuestSolutionAccept == true;
        var canAccept = supports && owner.Issue.IsOngoingWithoutQuest && owner.Issue.IssueStayAliveConditions();
        if (!canAccept)
        {
            network.Send(requester, new NetworkQuestTypeAcceptRejected(ownerId, isAlternative));
            return false;
        }

        return true;
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

            if (!TryValidateAcceptRequest(requester, ownerId, owner, requestedGeneration, isAlternative: false,
                nameof(RequestQuestTypeAcceptQuest), out var player, out var descriptor))
            {
                return;
            }

            byte[] fieldsBytes = null;
            try
            {
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

            if (owner.Issue != null &&
                TryTakeLocalTransfers(owner.Issue.AlternativeSolutionSentTroops, out var localTransfer))
            {
                RevertLocalTransfers(MobileParty.MainParty, localTransfer.Before, localTransfer.After);
                using (new AllowedThread()) owner.Issue.AlternativeSolutionSentTroops.Clear();
            }
            if (owner.Issue != null) resetQuestScreens.Remove(owner.Issue.AlternativeSolutionSentTroops);
            ownershipRegistry.SetOwner(owner, data.OwnerControllerId);
            RollbackPendingAlternativeAccept(owner);
            acceptedAlternativeTroops.Remove(owner);
        });
    }

    private void Handle_QuestTypeAlternativeAcceptTriggered(MessagePayload<QuestTypeAlternativeAcceptTriggered> payload)
    {
        var owner = payload.What.Owner;
        if (owner == null || !objectManager.TryGetIdWithLogging(owner, out var ownerId)) return;

        var descriptor = QuestTypeRegistry.Get(owner.Issue);
        if (descriptor?.SupportsAlternativeAccept != true) return;
        if (ownershipRegistry.TryGetOwnerControllerId(owner, out _))
        {
            if (ModInformation.IsClient && payload.What.SelectedTroops != null)
            {
                if (TryTakeLocalTransfers(owner.Issue.AlternativeSolutionSentTroops, out var transfer))
                    RevertLocalTransfers(MobileParty.MainParty, transfer.Before, transfer.After);
                if (acceptedAlternativeTroops.TryGetValue(owner, out var winningTroops) &&
                    ReferenceEquals(owner.Issue, winningTroops.Issue))
                    ApplyReceivedTroops(owner, winningTroops.Troops);
                else
                    using (new AllowedThread()) owner.Issue.AlternativeSolutionSentTroops.Clear();
            }
            return;
        }

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
            if (pendingAlternativeAccepts.TryGetValue(owner, out var pending) &&
                !ReferenceEquals(pending.Issue, owner.Issue))
                RollbackPendingAlternativeAccept(owner);
            if (pendingAlternativeAccepts.ContainsKey(owner))
            {
                if (TryTakeLocalTransfers(owner.Issue.AlternativeSolutionSentTroops, out var transfer))
                    RevertLocalTransfers(MobileParty.MainParty, transfer.Before, transfer.After);
                else if (!pendingAlternativeAccepts[owner].ResetObserved)
                    RestoreSelectedTroops(MobileParty.MainParty, payload.What.SelectedTroops);
                using (new AllowedThread()) owner.Issue.AlternativeSolutionSentTroops.Clear();
                return;
            }
            generationRegistry.TryGetGeneration(owner, out var generation);
            var roster = owner.Issue.AlternativeSolutionSentTroops;
            var resetObserved = resetQuestScreens.TryGetValue(roster, out var reset) &&
                (payload.What.Screen == null || ReferenceEquals(payload.What.Screen, reset.Screen));
            resetQuestScreens.Remove(roster);
            var selectedTroops = TroopRoster.CreateDummyTroopRoster();
            selectedTroops.Add(payload.What.SelectedTroops?.TotalManCount > 0 ? payload.What.SelectedTroops :
                resetObserved && reset.SelectedTroops != null ? reset.SelectedTroops : roster);
            pendingAlternativeAccepts[owner] = (owner.Issue, MobileParty.MainParty, selectedTroops,
                roster, payload.What.Screen ?? reset.Screen, resetObserved);
            localSelectionTransfers.Remove(roster);
            var packedTroops = troopRosterInterface.PackTroopRosterData(selectedTroops);
            network.SendAll(new RequestQuestTypeAcceptAlternative(ownerId, generation, packedTroops));
            using (new AllowedThread()) owner.Issue.AlternativeSolutionSentTroops.Clear();
        }
    }

    private void Handle_QuestAlternativeTroopsTransferredLocally(MessagePayload<QuestAlternativeTroopsTransferredLocally> payload)
    {
        var transfer = payload.What;
        if (transfer.Roster == null) return;
        var before = localSelectionTransfers.TryGetValue(transfer.Roster, out var previous)
            ? previous.Before : transfer.Before;
        localSelectionTransfers[transfer.Roster] = (before, transfer.After);
    }

    private void Handle_QuestAlternativeTroopSelectionClosed(MessagePayload<QuestAlternativeTroopSelectionClosed> payload)
    {
        if (payload.What.Roster == null) return;
        localSelectionTransfers.Remove(payload.What.Roster);
        resetQuestScreens.Remove(payload.What.Roster);
    }

    private void Handle_QuestAlternativeTroopSelectionReset(MessagePayload<QuestAlternativeTroopSelectionReset> payload)
    {
        if (payload.What.Roster == null) return;
        localSelectionTransfers.Remove(payload.What.Roster);
        resetQuestScreens[payload.What.Roster] = (payload.What.Screen, payload.What.SelectedTroops);
        Hero pendingOwner = null;
        foreach (var pair in pendingAlternativeAccepts)
        {
            if (!ReferenceEquals(pair.Value.Roster, payload.What.Roster)) continue;
            pendingOwner = pair.Key;
            break;
        }
        if (pendingOwner == null)
        {
            foreach (var accepted in acceptedAlternativeTroops)
            {
                if (ReferenceEquals(accepted.Key.Issue, accepted.Value.Issue) &&
                    ReferenceEquals(accepted.Value.Issue.AlternativeSolutionSentTroops, payload.What.Roster))
                {
                    ApplyReceivedTroops(accepted.Key, accepted.Value.Troops);
                    break;
                }
            }
            return;
        }
        var pending = pendingAlternativeAccepts[pendingOwner];
        if (pending.Screen == null || !ReferenceEquals(pending.Screen, payload.What.Screen)) return;
        if (pending.ResetObserved) return;
        pendingAlternativeAccepts[pendingOwner] = (pending.Issue, pending.Party, pending.Troops,
            pending.Roster, pending.Screen, true);
    }

    private bool TryTakeLocalTransfers(TroopRoster roster, out (TroopRoster Before, TroopRoster After) transfer)
    {
        transfer = default;
        if (roster == null || !localSelectionTransfers.TryGetValue(roster, out transfer)) return false;
        localSelectionTransfers.Remove(roster);
        return true;
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

            if (TryTakeLocalTransfers(owner.Issue.AlternativeSolutionSentTroops, out var localTransfer))
                RevertLocalTransfers(MobileParty.MainParty, localTransfer.Before, localTransfer.After);
            resetQuestScreens.Remove(owner.Issue.AlternativeSolutionSentTroops);
            ownershipRegistry.SetOwner(owner, data.OwnerControllerId);
            acceptedAlternativeTroops[owner] = (owner.Issue, data.SentTroops);
            if (ownershipRegistry.IsLocalPeerOwner(owner))
                pendingAlternativeAccepts.Remove(owner);
            else
                RollbackPendingAlternativeAccept(owner);
        });
    }

    private static void RestoreSelectedTroops(MobileParty party, TroopRoster troops)
    {
        if (troops == null) return;
        using (new AllowedThread())
        {
            foreach (var element in troops.GetTroopRoster())
            {
                if (party != null)
                    ApplyLocalTransferDelta(party, element.Character, element.Number,
                        element.WoundedNumber, element.Xp);
                else if (element.Character.IsHero)
                    element.Character.HeroObject.ChangeState(Hero.CharacterStates.Active);
            }
        }
    }

    private static void RevertLocalTransfers(MobileParty party, TroopRoster before, TroopRoster after)
    {
        if (party == null) return;
        var seen = new HashSet<CharacterObject>();
        using (new AllowedThread())
        {
            foreach (var element in before.GetTroopRoster())
            {
                seen.Add(element.Character);
                var index = after.FindIndexOfTroop(element.Character);
                var current = index >= 0 ? after.GetElementCopyAtIndex(index) : default;
                ApplyLocalTransferDelta(party, element.Character,
                    current.Number - element.Number,
                    current.WoundedNumber - element.WoundedNumber,
                    current.Xp - element.Xp);
            }
            foreach (var element in after.GetTroopRoster())
            {
                if (!seen.Add(element.Character)) continue;
                ApplyLocalTransferDelta(party, element.Character,
                    element.Number, element.WoundedNumber, element.Xp);
            }
        }
    }

    private static void ApplyLocalTransferDelta(MobileParty party, CharacterObject character,
        int count, int wounded, int xp)
    {
        if (count == 0 && wounded == 0 && xp == 0) return;
        var restoredByActivation = 0;
        if (count > 0 && character.IsHero && character.HeroObject.HeroState != Hero.CharacterStates.Active)
        {
            var beforeActivation = party.MemberRoster.GetTroopCount(character);
            character.HeroObject.ChangeState(Hero.CharacterStates.Active);
            restoredByActivation = party.MemberRoster.GetTroopCount(character) - beforeActivation;
        }
        party.MemberRoster.AddToCounts(character, count - restoredByActivation, false, wounded, xp, true);
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

    private void RollbackPendingAlternativeAccept(Hero owner)
    {
        if (!pendingAlternativeAccepts.TryGetValue(owner, out var pending)) return;
        pendingAlternativeAccepts.Remove(owner);
        if (TryTakeLocalTransfers(pending.Roster, out var laterTransfer))
            RevertLocalTransfers(pending.Party, laterTransfer.Before, laterTransfer.After);
        if (!pending.ResetObserved) RestoreSelectedTroops(pending.Party, pending.Troops);

        using (new AllowedThread())
        {
            var hasWinner = ownershipRegistry.TryGetOwnerControllerId(owner, out _);
            if (ReferenceEquals(owner.Issue, pending.Issue) && (!hasWinner || !owner.Issue.IsSolvingWithAlternative))
            {
                owner.Issue.AlternativeSolutionSentTroops.Clear();
            }
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

            var resetRoster = owner.Issue?.AlternativeSolutionSentTroops;
            var descriptor = QuestTypeRegistry.Get(owner.Issue);
            if (isAlternative)
            {
                if (!ownershipRegistry.TryGetOwnerControllerId(owner, out _))
                    descriptor?.RejectAlternativeAccept?.Invoke(owner);
                RollbackPendingAlternativeAccept(owner);
            }
            else if (descriptor?.RejectQuestSolutionAccept != null)
            {
                descriptor.RejectQuestSolutionAccept(owner);
            }
            else
            {
                AcceptMirrorSupport.RejectAcceptance(owner);
            }
            if (resetRoster != null) resetQuestScreens.Remove(resetRoster);
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
