using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using Common.Util;
using GameInterface.Services.Inventory.Data;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TroopRosterElementData = GameInterface.Services.TroopRosters.Data.TroopRosterElementData;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.Barter;
using TaleWorlds.Core;
using Helpers;
using TaleWorlds.Library;

namespace GameInterface.Services.MapEvents.PlayerPartyInteractions;

internal class PlayerPartyInteractionHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<PlayerPartyInteractionHandler>();
    private static readonly FieldInfo PrisonerCharacterField =
        typeof(TransferPrisonerBarterable).GetField("_prisonerCharacter", BindingFlags.Instance | BindingFlags.NonPublic);

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly ConversationPartyTracker conversationPartyTracker;
    private readonly INetworkConfig configuration;
    private readonly IPlayerPartyHostileEncounterService hostileEncounterService;
    private readonly PlayerPartyInteractionOutcomeHandler outcomeHandler;

    private readonly ConcurrentDictionary<string, PlayerPartyInteractionSession> sessionsById = new ConcurrentDictionary<string, PlayerPartyInteractionSession>();
    private readonly ConcurrentDictionary<string, string> sessionsByPartyId = new ConcurrentDictionary<string, string>();
    private readonly HashSet<string> openedConversationSessionIds = new HashSet<string>();
    private readonly HashSet<string> hostileEncounterSessionIds = new HashSet<string>();

    public PlayerPartyInteractionHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        ConversationPartyTracker conversationPartyTracker,
        INetworkConfig configuration,
        IPlayerPartyHostileEncounterService hostileEncounterService)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.conversationPartyTracker = conversationPartyTracker;
        this.configuration = configuration;
        this.hostileEncounterService = hostileEncounterService;
        outcomeHandler = new PlayerPartyInteractionOutcomeHandler(objectManager);

        messageBroker.Subscribe<NetworkPlayerPartyInteractionStarted>(Handle_NetworkPlayerPartyInteractionStarted);
        messageBroker.Subscribe<NetworkPlayerPartyInteractionState>(Handle_NetworkPlayerPartyInteractionState);
        messageBroker.Subscribe<NetworkSubmitPlayerPartyInteractionOption>(Handle_NetworkSubmitPlayerPartyInteractionOption);
        messageBroker.Subscribe<NetworkPlayerPartyInteractionShown>(Handle_NetworkPlayerPartyInteractionShown);
        messageBroker.Subscribe<NetworkPlayerPartyInteractionEnded>(Handle_NetworkPlayerPartyInteractionEnded);
        messageBroker.Subscribe<NetworkPlayerPartyInteractionDenied>(Handle_NetworkPlayerPartyInteractionDenied);
        messageBroker.Subscribe<NetworkPlayerPartyHostileEncounterStarted>(Handle_NetworkPlayerPartyHostileEncounterStarted);
        messageBroker.Subscribe<PlayerPartyInteractionOptionSelected>(Handle_PlayerPartyInteractionOptionSelected);
        messageBroker.Subscribe<PlayerPartyTradeOfferChanged>(Handle_PlayerPartyTradeOfferChanged);
        messageBroker.Subscribe<PlayerPartyTradeAcceptSelected>(Handle_PlayerPartyTradeAcceptSelected);
        messageBroker.Subscribe<NetworkPlayerPartyTradeOfferUpdated>(Handle_NetworkPlayerPartyTradeOfferUpdated);
        messageBroker.Subscribe<NetworkPlayerPartyTradeAcceptChanged>(Handle_NetworkPlayerPartyTradeAcceptChanged);
        messageBroker.Subscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkPlayerPartyInteractionStarted>(Handle_NetworkPlayerPartyInteractionStarted);
        messageBroker.Unsubscribe<NetworkPlayerPartyInteractionState>(Handle_NetworkPlayerPartyInteractionState);
        messageBroker.Unsubscribe<NetworkSubmitPlayerPartyInteractionOption>(Handle_NetworkSubmitPlayerPartyInteractionOption);
        messageBroker.Unsubscribe<NetworkPlayerPartyInteractionShown>(Handle_NetworkPlayerPartyInteractionShown);
        messageBroker.Unsubscribe<NetworkPlayerPartyInteractionEnded>(Handle_NetworkPlayerPartyInteractionEnded);
        messageBroker.Unsubscribe<NetworkPlayerPartyInteractionDenied>(Handle_NetworkPlayerPartyInteractionDenied);
        messageBroker.Unsubscribe<NetworkPlayerPartyHostileEncounterStarted>(Handle_NetworkPlayerPartyHostileEncounterStarted);
        messageBroker.Unsubscribe<PlayerPartyInteractionOptionSelected>(Handle_PlayerPartyInteractionOptionSelected);
        messageBroker.Unsubscribe<PlayerPartyTradeOfferChanged>(Handle_PlayerPartyTradeOfferChanged);
        messageBroker.Unsubscribe<PlayerPartyTradeAcceptSelected>(Handle_PlayerPartyTradeAcceptSelected);
        messageBroker.Unsubscribe<NetworkPlayerPartyTradeOfferUpdated>(Handle_NetworkPlayerPartyTradeOfferUpdated);
        messageBroker.Unsubscribe<NetworkPlayerPartyTradeAcceptChanged>(Handle_NetworkPlayerPartyTradeAcceptChanged);
        messageBroker.Unsubscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
    }

    public bool TryStartSession(
        NetPeer initiatorPeer,
        NetworkRequestConversation request,
        PartyBase initiatorParty,
        PartyBase responderParty)
    {
        if (ModInformation.IsClient) return false;

        if (IsPartyBusy(request.AttackerId, request.DefenderId) || IsPartyBusy(request.DefenderId, request.AttackerId))
        {
            network.Send(initiatorPeer, new NetworkPlayerPartyInteractionDenied(PlayerPartyInteractionDeniedReason.Busy));
            return false;
        }

        var isHostile = AreHostile(initiatorParty, responderParty);

        var session = new PlayerPartyInteractionSession(
            Guid.NewGuid().ToString("N"),
            request.AttackerId,
            request.DefenderId,
            GetPartyName(initiatorParty, "Player"),
            GetPartyName(responderParty, "Player"),
            initiatorPeer,
            isHostile);

        AddInitialOptions(session, initiatorParty, responderParty);

        if (!sessionsById.TryAdd(session.SessionId, session))
            return false;

        sessionsByPartyId[session.InitiatorPartyId] = session.SessionId;
        sessionsByPartyId[session.ResponderPartyId] = session.SessionId;
        conversationPartyTracker.BeginPvpConversation(session.InitiatorPartyId, session.ResponderPartyId);

        GameThread.RunSafe(() =>
        {
            HoldParty(initiatorParty.MobileParty);
            HoldParty(responderParty.MobileParty);
        }, context: "Hold player-party interaction parties");

        network.SendAll(new NetworkPlayerPartyInteractionStarted(
            session.SessionId,
            session.InitiatorPartyId,
            session.ResponderPartyId,
            session.InitiatorName,
            session.ResponderName));

        SendInitialStates(session);

        return true;
    }

    private void Handle_NetworkPlayerPartyInteractionStarted(MessagePayload<NetworkPlayerPartyInteractionStarted> payload)
    {
        if (ModInformation.IsServer) return;

        var message = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!TryGetControlledSessionParty(message, out var myPartyId, out _)) return;

            network.SendAll(new NetworkPlayerPartyInteractionShown(message.SessionId, myPartyId));
        }, context: "Confirm player-party interaction party");
    }

    private void Handle_NetworkPlayerPartyInteractionState(MessagePayload<NetworkPlayerPartyInteractionState> payload)
    {
        if (ModInformation.IsServer) return;

        var message = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObject<PartyBase>(message.PartyId, out var party)) return;
            if (party.MobileParty?.IsControlledByThisInstance() != true) return;

            PlayerPartyInteractionDialogState.Apply(message);
            TryOpenMapConversation(message.SessionId, message.PartyId, message.OtherPartyId);

            if (message.Phase == PlayerPartyInteractionPhase.TradeActive)
            {
                var localAccepted = message.IsInitiator ? message.InitiatorAcceptedTrade : message.ResponderAcceptedTrade;
                var remoteAccepted = message.IsInitiator ? message.ResponderAcceptedTrade : message.InitiatorAcceptedTrade;
                OpenTrade(message);
                PlayerPartyTradeContext.UpdateAcceptance(localAccepted, remoteAccepted);
                PlayerPartyTradeOverlay.Instance.UpdateState(localAccepted, remoteAccepted);
            }
        }, context: "Apply player-party interaction state");
    }

    private void Handle_PlayerPartyInteractionOptionSelected(MessagePayload<PlayerPartyInteractionOptionSelected> payload)
    {
        if (ModInformation.IsServer) return;

        var message = payload.What;
        network.SendAll(new NetworkSubmitPlayerPartyInteractionOption(message.SessionId, message.Option, message.PartyId));
    }

    private void Handle_NetworkSubmitPlayerPartyInteractionOption(MessagePayload<NetworkSubmitPlayerPartyInteractionOption> payload)
    {
        if (ModInformation.IsClient) return;
        if (!(payload.Who is NetPeer peer)) return;

        var message = payload.What;
        if (!sessionsById.TryGetValue(message.SessionId, out var session)) return;
        if (!TryGetSessionPartyId(session, peer, out var partyId)) return;

        if (partyId == session.InitiatorPartyId)
        {
            HandleInitiatorOption(session, message.Option);
            return;
        }

        if (partyId == session.ResponderPartyId)
        {
            HandleResponderOption(session, message.Option);
            return;
        }
    }

    private void Handle_NetworkPlayerPartyInteractionShown(MessagePayload<NetworkPlayerPartyInteractionShown> payload)
    {
        if (ModInformation.IsClient) return;
        if (!(payload.Who is NetPeer peer)) return;

        var message = payload.What;
        if (!sessionsById.TryGetValue(message.SessionId, out var session)) return;

        if (message.PartyId == session.ResponderPartyId &&
            !ReferenceEquals(peer, session.InitiatorPeer) &&
            session.ResponderPeer == null)
            session.ResponderPeer = peer;
    }

    private void Handle_NetworkPlayerPartyInteractionEnded(MessagePayload<NetworkPlayerPartyInteractionEnded> payload)
    {
        if (ModInformation.IsServer) return;

        var message = payload.What;

        GameThread.RunSafe(() =>
        {
            var isLocalInteraction = TryGetLocalInteractionParty(message, out var localParty);
            if (!isLocalInteraction && !IsCurrentLocalInteractionSession(message.SessionId)) return;

            PlayerPartyInteractionDialogState.Clear(message.SessionId);
            PlayerPartyTradeContext.End(message.SessionId, message.OutcomeType);
            PlayerPartyTradeOverlay.Instance.Hide(message.SessionId);
            openedConversationSessionIds.Remove(message.SessionId);

            var conversationManager = Campaign.Current?.ConversationManager;
            if (conversationManager?.IsConversationInProgress == true)
                conversationManager.EndConversation();

            var hostileEncounterStarted = hostileEncounterSessionIds.Remove(message.SessionId);
            if (message.OutcomeType != PlayerPartyInteractionOutcomeType.HostileDemandAccepted || !hostileEncounterStarted)
                CloseLocalPlayerPartyEncounter(localParty?.MobileParty);
        }, context: "End player-party interaction");
    }

    private void Handle_NetworkPlayerPartyInteractionDenied(MessagePayload<NetworkPlayerPartyInteractionDenied> payload)
    {
        if (ModInformation.IsServer) return;

        if (payload.What.Reason == PlayerPartyInteractionDeniedReason.Hostile)
            return;

        GameThread.RunSafe(
            ConversationPartyHold.ShowInteractionBlockedMessage,
            context: "Show player-party interaction denied");
    }

    private void Handle_NetworkPlayerPartyHostileEncounterStarted(MessagePayload<NetworkPlayerPartyHostileEncounterStarted> payload)
    {
        if (ModInformation.IsServer) return;

        var message = payload.What;
        GameThread.RunSafe(
            () => TryOpenHostileEncounter(message),
            context: "Open player-party hostile encounter");
    }

    private void Handle_PlayerPartyTradeOfferChanged(MessagePayload<PlayerPartyTradeOfferChanged> payload)
    {
        if (ModInformation.IsServer) return;

        var message = payload.What;
        if (!TryGetLocalPartyId(out var partyId)) return;

        var offeredItems = message.InventoryLogic != null
            ? ResolveItemIds(message.InventoryLogic.GetSoldItems())
            : ResolveItemIds(GetOfferedBarterItems(message.BarterVM));
        var offeredGold = message.BarterVM != null
            ? GetOfferedGold(message.BarterVM)
            : 0;
        var offeredFiefs = message.BarterVM != null
            ? ResolveSettlementIds(GetOfferedBarterFiefs(message.BarterVM))
            : Array.Empty<string>();
        var offeredPrisoners = message.BarterVM != null
            ? ResolveCharacterIds(GetOfferedBarterPrisoners(message.BarterVM))
            : Array.Empty<TroopRosterElementData>();
        var offeredTroops = message.BarterVM != null
            ? ResolveTroopIds(GetOfferedBarterTroops(message.BarterVM))
            : Array.Empty<TroopRosterElementData>();
        var offeredPeace = message.BarterVM != null && HasOfferedPeace(message.BarterVM);
        network.SendAll(new NetworkPlayerPartyTradeOfferUpdated(
            message.SessionId,
            partyId,
            offeredItems,
            offeredTroops,
            offeredGold,
            offeredFiefs,
            offeredPrisoners,
            offeredPeace));
    }

    private void Handle_PlayerPartyTradeAcceptSelected(MessagePayload<PlayerPartyTradeAcceptSelected> payload)
    {
        if (ModInformation.IsServer) return;

        var message = payload.What;
        network.SendAll(new NetworkPlayerPartyTradeAcceptChanged(message.SessionId, message.Accepted));
    }

    private void Handle_NetworkPlayerPartyTradeOfferUpdated(MessagePayload<NetworkPlayerPartyTradeOfferUpdated> payload)
    {
        if (ModInformation.IsClient)
        {
            var clientMessage = payload.What;
            GameThread.RunSafe(
                () => PlayerPartyTradeContext.ApplyOfferUpdate(clientMessage, objectManager),
                context: "Apply player-party trade offer update");
            return;
        }

        if (!(payload.Who is NetPeer peer)) return;

        var message = payload.What;
        if (!sessionsById.TryGetValue(message.SessionId, out var session)) return;
        if (!TryGetSessionPartyId(session, peer, out var partyId)) return;

        var offeredPeace = message.OfferedPeace && CanOfferPeace(session, partyId);
        session.SetTradeOffer(
            partyId,
            message.OfferedItems,
            message.OfferedTroops,
            message.OfferedGold,
            message.OfferedFiefs,
            message.OfferedPrisoners,
            offeredPeace);
        session.InitiatorAcceptedTrade = false;
        session.ResponderAcceptedTrade = false;
        network.SendAll(new NetworkPlayerPartyTradeOfferUpdated(
            message.SessionId,
            partyId,
            message.OfferedItems,
            message.OfferedTroops,
            message.OfferedGold,
            message.OfferedFiefs,
            message.OfferedPrisoners,
            offeredPeace));
        SendTradeStates(session, false);
    }

    private bool CanOfferPeace(PlayerPartyInteractionSession session, string partyId)
    {
        var otherPartyId = session.GetOtherPartyId(partyId);
        if (!objectManager.TryGetObject<PartyBase>(partyId, out var party)) return false;
        if (!objectManager.TryGetObject<PartyBase>(otherPartyId, out var otherParty)) return false;

        return PlayerPartyPeaceBarterable.CanOfferPeace(party, otherParty);
    }

    private void Handle_NetworkPlayerPartyTradeAcceptChanged(MessagePayload<NetworkPlayerPartyTradeAcceptChanged> payload)
    {
        if (ModInformation.IsClient) return;
        if (!(payload.Who is NetPeer peer)) return;

        var message = payload.What;
        if (!sessionsById.TryGetValue(message.SessionId, out var session)) return;
        if (!TryGetSessionPartyId(session, peer, out var partyId)) return;

        if (partyId == session.InitiatorPartyId)
            session.InitiatorAcceptedTrade = message.Accepted;
        else if (partyId == session.ResponderPartyId)
            session.ResponderAcceptedTrade = message.Accepted;
        else
            return;

        SendTradeStates(session);

        if (session.InitiatorAcceptedTrade && session.ResponderAcceptedTrade)
            EndSession(session, PlayerPartyInteractionOutcomeType.TradeAccepted);
    }

    private void Handle_PlayerDisconnected(MessagePayload<PlayerDisconnected> payload)
    {
        if (ModInformation.IsClient) return;

        var peer = payload.What.PlayerId;
        foreach (var session in sessionsById.Values.ToArray())
        {
            if (session.InitiatorPeer == peer || session.ResponderPeer == peer)
                EndSession(session, PlayerPartyInteractionOutcomeType.Disconnected);
        }
    }

    private void HandleInitiatorOption(PlayerPartyInteractionSession session, PlayerPartyInteractionOption option)
    {
        if (option == PlayerPartyInteractionOption.Leave)
        {
            if (session.Proposal == PlayerPartyInteractionProposal.HostileDemand && session.HostileDemandConfirmed)
                return;

            EndSession(session, GetLeaveOutcome(session));
            return;
        }

        if (option == PlayerPartyInteractionOption.OfferServices)
        {
            return;
        }

        if (option == PlayerPartyInteractionOption.HostileDemand)
        {
            if (!session.InitiatorEnabledOptions.Contains(option)) return;

            session.Proposal = PlayerPartyInteractionProposal.HostileDemand;
            session.HostileDemandConfirmed = false;
            SendInitiatorState(
                session,
                PlayerPartyInteractionPhase.HostileDemandConfirm,
                session.Proposal,
                new[]
                {
                    PlayerPartyInteractionOption.ConfirmHostileDemand,
                    PlayerPartyInteractionOption.CancelHostileDemand
                });
            return;
        }

        if (option == PlayerPartyInteractionOption.ConfirmHostileDemand)
        {
            if (session.Proposal != PlayerPartyInteractionProposal.HostileDemand) return;
            if (session.HostileDemandConfirmed) return;

            session.HostileDemandConfirmed = true;
            SendInitiatorState(session, PlayerPartyInteractionPhase.WaitingForResponse, session.Proposal, Array.Empty<PlayerPartyInteractionOption>());
            SendResponderState(
                session,
                PlayerPartyInteractionPhase.HostileDemandPending,
                session.Proposal,
                new[]
                {
                    PlayerPartyInteractionOption.RefuseHostileDemand,
                    PlayerPartyInteractionOption.YieldHostileDemand
                });
            return;
        }

        if (option == PlayerPartyInteractionOption.CancelHostileDemand)
        {
            if (session.Proposal != PlayerPartyInteractionProposal.HostileDemand) return;
            if (session.HostileDemandConfirmed) return;

            EndSession(session, PlayerPartyInteractionOutcomeType.Left);
            return;
        }

        var proposal = ToProposal(option);
        if (proposal == PlayerPartyInteractionProposal.None) return;
        if (!session.InitiatorEnabledOptions.Contains(option)) return;

        session.Proposal = proposal;
        SendInitiatorState(session, PlayerPartyInteractionPhase.WaitingForResponse, proposal, Array.Empty<PlayerPartyInteractionOption>());
        SendResponderState(
            session,
            PlayerPartyInteractionPhase.ProposalPending,
            proposal,
            new[]
            {
                PlayerPartyInteractionOption.AcceptProposal,
                PlayerPartyInteractionOption.DeclineProposal,
                PlayerPartyInteractionOption.Leave
            });
    }

    private void HandleResponderOption(PlayerPartyInteractionSession session, PlayerPartyInteractionOption option)
    {
        if (option == PlayerPartyInteractionOption.YieldHostileDemand)
        {
            if (session.Proposal != PlayerPartyInteractionProposal.HostileDemand) return;
            if (!session.HostileDemandConfirmed) return;

            EndSession(session, PlayerPartyInteractionOutcomeType.HostileDemandYielded);
            return;
        }

        if (option == PlayerPartyInteractionOption.RefuseHostileDemand)
        {
            if (session.Proposal != PlayerPartyInteractionProposal.HostileDemand) return;
            if (!session.HostileDemandConfirmed) return;

            EndSession(session, PlayerPartyInteractionOutcomeType.HostileDemandAccepted);
            return;
        }

        if (option == PlayerPartyInteractionOption.Leave)
        {
            if (session.Proposal == PlayerPartyInteractionProposal.HostileDemand && session.HostileDemandConfirmed)
                return;

            EndSession(session, GetLeaveOutcome(session));
            return;
        }

        if (option == PlayerPartyInteractionOption.DeclineProposal)
        {
            EndSession(session, GetDeclinedOutcome(session.Proposal));
            return;
        }

        if (option != PlayerPartyInteractionOption.AcceptProposal) return;

        if (session.Proposal == PlayerPartyInteractionProposal.Trade)
        {
            session.InitiatorAcceptedTrade = false;
            session.ResponderAcceptedTrade = false;
            SendTradeStates(session);
            return;
        }

        EndSession(session, GetAcceptedOutcome(session.Proposal));
    }

    private void SendInitialStates(PlayerPartyInteractionSession session)
    {
        SendInitiatorState(
            session,
            PlayerPartyInteractionPhase.InitialOptions,
            PlayerPartyInteractionProposal.None,
            session.InitiatorOptions.ToArray(),
            session.InitiatorEnabledOptions.ToArray());

        SendResponderState(
            session,
            PlayerPartyInteractionPhase.WaitingForProposal,
            PlayerPartyInteractionProposal.None,
            new[] { PlayerPartyInteractionOption.Leave });
    }

    private void SendTradeStates(PlayerPartyInteractionSession session)
        => SendTradeStates(session, true);

    private void SendTradeStates(PlayerPartyInteractionSession session, bool sendOffers)
    {
        SendInitiatorState(session, PlayerPartyInteractionPhase.TradeActive, session.Proposal, Array.Empty<PlayerPartyInteractionOption>());
        SendResponderState(session, PlayerPartyInteractionPhase.TradeActive, session.Proposal, Array.Empty<PlayerPartyInteractionOption>());

        if (sendOffers)
            SendTradeOffers(session);
    }

    private void SendTradeOffers(PlayerPartyInteractionSession session)
    {
        network.SendAll(new NetworkPlayerPartyTradeOfferUpdated(
            session.SessionId,
            session.InitiatorPartyId,
            session.InitiatorOfferedItems,
            session.InitiatorOfferedTroops,
            session.InitiatorOfferedGold,
            session.InitiatorOfferedFiefs,
            session.InitiatorOfferedPrisoners,
            session.InitiatorOfferedPeace));

        network.SendAll(new NetworkPlayerPartyTradeOfferUpdated(
            session.SessionId,
            session.ResponderPartyId,
            session.ResponderOfferedItems,
            session.ResponderOfferedTroops,
            session.ResponderOfferedGold,
            session.ResponderOfferedFiefs,
            session.ResponderOfferedPrisoners,
            session.ResponderOfferedPeace));
    }

    private void SendInitiatorState(
        PlayerPartyInteractionSession session,
        PlayerPartyInteractionPhase phase,
        PlayerPartyInteractionProposal proposal,
        PlayerPartyInteractionOption[] options,
        PlayerPartyInteractionOption[] enabledOptions = null)
    {
        var partyItems = phase == PlayerPartyInteractionPhase.TradeActive
            ? ResolvePartyItemIds(session.InitiatorPartyId)
            : Array.Empty<ItemRosterElementData>();
        var otherPartyItems = phase == PlayerPartyInteractionPhase.TradeActive
            ? ResolvePartyItemIds(session.ResponderPartyId)
            : Array.Empty<ItemRosterElementData>();

        network.SendAll(new NetworkPlayerPartyInteractionState(
            session.SessionId,
            session.InitiatorPartyId,
            session.ResponderPartyId,
            session.ResponderName,
            phase,
            proposal,
            options,
            true,
            session.InitiatorAcceptedTrade,
            session.ResponderAcceptedTrade,
            partyItems,
            otherPartyItems,
            enabledOptions,
            session.IsHostile));
    }

    private void SendResponderState(
        PlayerPartyInteractionSession session,
        PlayerPartyInteractionPhase phase,
        PlayerPartyInteractionProposal proposal,
        PlayerPartyInteractionOption[] options,
        PlayerPartyInteractionOption[] enabledOptions = null)
    {
        var partyItems = phase == PlayerPartyInteractionPhase.TradeActive
            ? ResolvePartyItemIds(session.ResponderPartyId)
            : Array.Empty<ItemRosterElementData>();
        var otherPartyItems = phase == PlayerPartyInteractionPhase.TradeActive
            ? ResolvePartyItemIds(session.InitiatorPartyId)
            : Array.Empty<ItemRosterElementData>();

        network.SendAll(new NetworkPlayerPartyInteractionState(
            session.SessionId,
            session.ResponderPartyId,
            session.InitiatorPartyId,
            session.InitiatorName,
            phase,
            proposal,
            options,
            false,
            session.InitiatorAcceptedTrade,
            session.ResponderAcceptedTrade,
            partyItems,
            otherPartyItems,
            enabledOptions,
            session.IsHostile));
    }

    private void EndSession(PlayerPartyInteractionSession session, PlayerPartyInteractionOutcomeType outcomeType)
    {
        if (!sessionsById.TryRemove(session.SessionId, out _)) return;

        sessionsByPartyId.TryRemove(session.InitiatorPartyId, out _);
        sessionsByPartyId.TryRemove(session.ResponderPartyId, out _);
        conversationPartyTracker.EndPvpConversation(session.InitiatorPartyId);

        var outcome = new PlayerPartyInteractionOutcome(session, outcomeType);
        outcomeHandler.Handle(outcome);

        network.SendAll(new NetworkPlayerPartyInteractionEnded(
            session.SessionId,
            session.InitiatorPartyId,
            session.ResponderPartyId,
            outcomeType));

        if (outcomeType == PlayerPartyInteractionOutcomeType.HostileDemandAccepted ||
            outcomeType == PlayerPartyInteractionOutcomeType.HostileDemandYielded)
        {
            hostileEncounterService.TryStartHostileEncounter(
                session.SessionId,
                session.InitiatorPartyId,
                session.ResponderPartyId,
                outcomeType == PlayerPartyInteractionOutcomeType.HostileDemandYielded);
        }
    }

    private void AddInitialOptions(PlayerPartyInteractionSession session, PartyBase initiatorParty, PartyBase responderParty)
    {
        AddInitiatorOption(session, PlayerPartyInteractionOption.TradeProposal, enabled: true);
        AddInitiatorOption(session, PlayerPartyInteractionOption.OfferServices, enabled: !session.IsHostile);
        AddInitiatorOption(session, PlayerPartyInteractionOption.HostileDemand, hostileEncounterService.CanStartHostileEncounter(initiatorParty, responderParty));
        AddInitiatorOption(session, PlayerPartyInteractionOption.JoinClan, enabled: false);
        AddInitiatorOption(
            session,
            PlayerPartyInteractionOption.Vassal,
            IsVassalServiceAvailable(initiatorParty, responderParty));
        AddInitiatorOption(session, PlayerPartyInteractionOption.Leave, enabled: true);
    }

    private static void AddInitiatorOption(PlayerPartyInteractionSession session, PlayerPartyInteractionOption option, bool enabled)
    {
        session.InitiatorOptions.Add(option);
        if (enabled)
            session.InitiatorEnabledOptions.Add(option);
    }

    private static bool IsVassalServiceAvailable(PartyBase initiatorParty, PartyBase responderParty)
    {
        var initiatorClan = initiatorParty.LeaderHero?.Clan ?? initiatorParty.MobileParty?.ActualClan;
        var responderHero = responderParty.LeaderHero;

        return responderHero?.IsKingdomLeader == true && initiatorClan?.Tier == 2;
    }

    private static PlayerPartyInteractionProposal ToProposal(PlayerPartyInteractionOption option)
    {
        switch (option)
        {
            case PlayerPartyInteractionOption.TradeProposal:
                return PlayerPartyInteractionProposal.Trade;
            case PlayerPartyInteractionOption.JoinClan:
                return PlayerPartyInteractionProposal.JoinClan;
            case PlayerPartyInteractionOption.Vassal:
                return PlayerPartyInteractionProposal.Vassal;
            case PlayerPartyInteractionOption.HostileDemand:
                return PlayerPartyInteractionProposal.HostileDemand;
            default:
                return PlayerPartyInteractionProposal.None;
        }
    }

    private static PlayerPartyInteractionOutcomeType GetAcceptedOutcome(PlayerPartyInteractionProposal proposal)
    {
        switch (proposal)
        {
            case PlayerPartyInteractionProposal.JoinClan:
                return PlayerPartyInteractionOutcomeType.ClanJoinAccepted;
            case PlayerPartyInteractionProposal.Vassal:
                return PlayerPartyInteractionOutcomeType.VassalAccepted;
            default:
                return PlayerPartyInteractionOutcomeType.None;
        }
    }

    private static PlayerPartyInteractionOutcomeType GetDeclinedOutcome(PlayerPartyInteractionProposal proposal)
    {
        switch (proposal)
        {
            case PlayerPartyInteractionProposal.Trade:
                return PlayerPartyInteractionOutcomeType.TradeDeclined;
            case PlayerPartyInteractionProposal.JoinClan:
                return PlayerPartyInteractionOutcomeType.ClanJoinDeclined;
            case PlayerPartyInteractionProposal.Vassal:
                return PlayerPartyInteractionOutcomeType.VassalDeclined;
            default:
                return PlayerPartyInteractionOutcomeType.None;
        }
    }

    private static PlayerPartyInteractionOutcomeType GetLeaveOutcome(PlayerPartyInteractionSession session)
    {
        if (session?.Proposal == PlayerPartyInteractionProposal.Trade)
            return PlayerPartyInteractionOutcomeType.TradeDeclined;

        return PlayerPartyInteractionOutcomeType.Left;
    }

    private bool IsPartyBusy(string partyId, string allowedPartnerId)
    {
        if (!sessionsByPartyId.TryGetValue(partyId, out var existingSessionId))
            return conversationPartyTracker.TryGetPvpPartner(partyId, out var partner) && partner != allowedPartnerId;

        if (!sessionsById.TryGetValue(existingSessionId, out var existingSession))
            return false;

        return existingSession.GetOtherPartyId(partyId) != allowedPartnerId;
    }

    private static bool AreHostile(PartyBase initiatorParty, PartyBase responderParty)
    {
        var initiatorFaction = initiatorParty?.MapFaction;
        var responderFaction = responderParty?.MapFaction;

        if (initiatorFaction == null || responderFaction == null) return false;
        if (initiatorFaction == responderFaction) return false;

        return FactionManager.IsAtWarAgainstFaction(initiatorFaction, responderFaction) ||
               HasFactionWar(initiatorFaction, responderFaction) ||
               HasFactionWar(responderFaction, initiatorFaction);
    }

    private static bool HasFactionWar(IFaction faction, IFaction otherFaction)
    {
        try
        {
            return faction.FactionsAtWarWith?.Contains(otherFaction) == true;
        }
        catch (NullReferenceException)
        {
            return false;
        }
    }

    private static void HoldParty(MobileParty party)
    {
        if (party == null) return;

        party.SetMoveModeHold();
        MessageBroker.Instance.Publish(party.Ai, new PartyBehaviorChangeAttempted(party.Ai, AiBehavior.Hold, null, party.Position));
    }

    private static string GetPartyName(PartyBase party, string fallback)
        => party?.LeaderHero?.Name?.ToString() ?? party?.Name?.ToString() ?? fallback;

    private static bool TryGetSessionPartyId(PlayerPartyInteractionSession session, NetPeer peer, out string partyId)
    {
        partyId = null;

        if (ReferenceEquals(peer, session.InitiatorPeer))
        {
            partyId = session.InitiatorPartyId;
            return true;
        }

        if (session.ResponderPeer != null && ReferenceEquals(peer, session.ResponderPeer))
        {
            partyId = session.ResponderPartyId;
            return true;
        }

        return false;
    }

    private void TryOpenHostileEncounter(NetworkPlayerPartyHostileEncounterStarted message)
    {
        if (!TryResolveHostileEncounter(message, out var attacker, out var defender, out var mapEvent))
            return;

        var localSide = GetLocalHostileEncounterSide(attacker, defender);
        if (localSide == BattleSideEnum.None)
            return;

        if (IsCurrentLocalInteractionSession(message.SessionId))
            hostileEncounterSessionIds.Add(message.SessionId);

        OpenHostileEncounter(attacker, defender, mapEvent, localSide);
    }

    private bool TryResolveHostileEncounter(
        NetworkPlayerPartyHostileEncounterStarted message,
        out PartyBase attacker,
        out PartyBase defender,
        out MapEvent mapEvent)
    {
        attacker = null;
        defender = null;
        mapEvent = null;

        var resolvedAttacker = default(PartyBase);
        var resolvedDefender = default(PartyBase);
        var resolvedMapEvent = default(MapEvent);
        var deadline = DateTime.UtcNow + configuration.ObjectCreationTimeout;
        bool IsReady() =>
            objectManager.TryGetObject(message.AttackerPartyId, out resolvedAttacker) &&
            objectManager.TryGetObject(message.DefenderPartyId, out resolvedDefender) &&
            objectManager.TryGetObject(message.MapEventId, out resolvedMapEvent) &&
            IsHostileEncounterReady(resolvedMapEvent, resolvedAttacker, resolvedDefender);

        if (GameThread.WaitWhilePumping(IsReady, deadline))
        {
            attacker = resolvedAttacker;
            defender = resolvedDefender;
            mapEvent = resolvedMapEvent;
            return true;
        }
        Logger.Error(
            "Timed out waiting for player-party hostile encounter map event. SessionId={SessionId}, MapEventId={MapEventId}, AttackerPartyId={AttackerPartyId}, DefenderPartyId={DefenderPartyId}",
            message.SessionId,
            message.MapEventId,
            message.AttackerPartyId,
            message.DefenderPartyId);
        return false;
    }

    private static bool IsHostileEncounterReady(MapEvent mapEvent, PartyBase attacker, PartyBase defender)
    {
        if (mapEvent == null || attacker == null || defender == null)
            return false;

        if (attacker.MapEventSide == null || defender.MapEventSide == null)
            return false;

        return HasMapEventParty(attacker.MapEventSide, attacker) &&
               HasMapEventParty(defender.MapEventSide, defender) &&
               (mapEvent.AttackerSide == attacker.MapEventSide || mapEvent.DefenderSide == attacker.MapEventSide) &&
               (mapEvent.AttackerSide == defender.MapEventSide || mapEvent.DefenderSide == defender.MapEventSide);
    }

    private static bool HasMapEventParty(MapEventSide side, PartyBase party)
        => side?.Parties?.Any(p => p.Party == party) == true;

    private static void OpenHostileEncounter(PartyBase attacker, PartyBase defender, MapEvent mapEvent, BattleSideEnum localSide)
    {
        if (PlayerEncounter.Current != null && PlayerEncounter.Battle != mapEvent)
            PlayerEncounter.Finish(true);

        using (new AllowedThread())
        {
            EncounterManager.RestartPlayerEncounter(attacker, defender);
        }

        AssignLocalHostileEncounter(attacker, defender, mapEvent, localSide);
    }

    private static BattleSideEnum GetLocalHostileEncounterSide(PartyBase attacker, PartyBase defender)
    {
        if (attacker.MobileParty?.IsControlledByThisInstance() == true)
            return BattleSideEnum.Attacker;

        if (defender.MobileParty?.IsControlledByThisInstance() == true)
            return BattleSideEnum.Defender;

        return BattleSideEnum.None;
    }

    private static void AssignLocalHostileEncounter(PartyBase attacker, PartyBase defender, MapEvent mapEvent, BattleSideEnum localSide)
    {
        var encounter = PlayerEncounter.Current;
        if (encounter == null) return;
        if (localSide != BattleSideEnum.Attacker && localSide != BattleSideEnum.Defender) return;

        encounter._attackerParty = attacker;
        encounter._defenderParty = defender;
        encounter._encounteredParty = localSide == BattleSideEnum.Attacker ? defender : attacker;
        encounter._mapEvent = mapEvent;
        encounter.PlayerSide = localSide;
        encounter.OpponentSide = localSide == BattleSideEnum.Attacker ? BattleSideEnum.Defender : BattleSideEnum.Attacker;
        encounter.IsJoinedBattle = true;

        GameMenu.SwitchToMenu("encounter");
    }

    private bool TryGetControlledSessionParty(
        NetworkPlayerPartyInteractionStarted message,
        out string myPartyId,
        out string otherPartyId)
    {
        myPartyId = null;
        otherPartyId = null;

        if (objectManager.TryGetObject<PartyBase>(message.InitiatorPartyId, out var initiatorParty) &&
            initiatorParty.MobileParty?.IsControlledByThisInstance() == true)
        {
            myPartyId = message.InitiatorPartyId;
            otherPartyId = message.ResponderPartyId;
            return true;
        }

        if (objectManager.TryGetObject<PartyBase>(message.ResponderPartyId, out var responderParty) &&
            responderParty.MobileParty?.IsControlledByThisInstance() == true)
        {
            myPartyId = message.ResponderPartyId;
            otherPartyId = message.InitiatorPartyId;
            return true;
        }

        return false;
    }

    private bool TryGetLocalInteractionParty(NetworkPlayerPartyInteractionEnded message, out PartyBase localParty)
    {
        localParty = null;

        if (objectManager.TryGetObject<PartyBase>(message.InitiatorPartyId, out var initiatorParty) &&
            initiatorParty.MobileParty?.IsControlledByThisInstance() == true)
        {
            localParty = initiatorParty;
            return true;
        }

        if (objectManager.TryGetObject<PartyBase>(message.ResponderPartyId, out var responderParty) &&
            responderParty.MobileParty?.IsControlledByThisInstance() == true)
        {
            localParty = responderParty;
            return true;
        }

        return false;
    }

    private static bool IsCurrentLocalInteractionSession(string sessionId)
        => PlayerPartyInteractionDialogState.SessionId == sessionId ||
           PlayerPartyTradeContext.SessionId == sessionId;

    private static void CloseLocalPlayerPartyEncounter(MobileParty localParty)
    {
        if (PlayerEncounter.Current != null)
            PlayerEncounter.Finish(true);

        if (Campaign.Current?.CurrentMenuContext != null)
            GameMenu.ExitToLast();

        ClearLocalPartyEngageOrder(localParty);
    }

    private static void ClearLocalPartyEngageOrder(MobileParty party)
    {
        if (party?.Ai == null) return;
        if (party.MapEvent != null) return;

        party.SetMoveModeHold();
        MessageBroker.Instance.Publish(party.Ai, new PartyBehaviorChangeAttempted(party.Ai, AiBehavior.Hold, null, party.Position));
    }

    private bool TryGetLocalPartyId(out string partyId)
    {
        partyId = PlayerPartyInteractionDialogState.PartyId;
        return !string.IsNullOrEmpty(partyId);
    }

    private void TryOpenMapConversation(string sessionId, string myPartyId, string otherPartyId)
    {
        if (openedConversationSessionIds.Contains(sessionId)) return;

        if (!objectManager.TryGetObject<PartyBase>(myPartyId, out var myParty)) return;
        if (!objectManager.TryGetObject<PartyBase>(otherPartyId, out var otherParty)) return;

        var myCharacter = myParty.LeaderHero?.CharacterObject;
        var otherCharacter = otherParty.LeaderHero?.CharacterObject;
        if (myCharacter == null || otherCharacter == null) return;

        var conversationManager = Campaign.Current?.ConversationManager;
        if (conversationManager == null) return;
        if (conversationManager.IsConversationInProgress) return;

        var playerData = new ConversationCharacterData(myCharacter, myParty, false, false, false, false, false, false);
        var otherData = new ConversationCharacterData(otherCharacter, otherParty, false, false, false, false, false, false);

        try
        {
            conversationManager.OpenMapConversation(playerData, otherData);
            PlayerPartyInteractionDialogState.RefreshConversation();
            openedConversationSessionIds.Add(sessionId);
        }
        catch (NullReferenceException ex)
        {
            Logger.Warning(ex, "Unable to open player party map conversation view");
        }
    }

    private void OpenTrade(NetworkPlayerPartyInteractionState state)
    {
        if (PlayerPartyTradeContext.IsActive) return;
        if (!objectManager.TryGetObject<PartyBase>(state.PartyId, out var myParty)) return;
        if (!objectManager.TryGetObject<PartyBase>(state.OtherPartyId, out var otherParty)) return;

        PlayerPartyTradeContext.Begin(state.SessionId, myParty);

        try
        {
            PlayerPartyTradeOverlay.Instance.Show(state.SessionId, state.OtherPlayerName);
        }
        catch (NullReferenceException ex)
        {
            Logger.Warning(ex, "Unable to open player party trade overlay");
        }

        try
        {
            ApplyTradeItemSnapshots(myParty, otherParty, state);
            OpenBarter(myParty, otherParty);
        }
        catch (NullReferenceException ex)
        {
            Logger.Warning(ex, "Unable to open player party barter screen");
        }
    }

    private void ApplyTradeItemSnapshots(PartyBase myParty, PartyBase otherParty, NetworkPlayerPartyInteractionState state)
    {
        ApplyTradeItemSnapshot(myParty?.ItemRoster, state.PartyItems);
        ApplyTradeItemSnapshot(otherParty?.ItemRoster, state.OtherPartyItems);
    }

    private void ApplyTradeItemSnapshot(ItemRoster itemRoster, ItemRosterElementData[] items)
    {
        if (itemRoster == null || items == null) return;

        using (new AllowedThread())
        {
            itemRoster.Clear();

            foreach (var item in items)
            {
                if (item.Amount <= 0) continue;
                if (!TryResolveEquipmentElement(item.ItemObjectData, out var equipmentElement)) continue;

                itemRoster.AddToCounts(equipmentElement, item.Amount);
            }
        }
    }

    private bool TryResolveEquipmentElement(ItemObjectData itemObjectData, out EquipmentElement equipmentElement)
    {
        equipmentElement = default;

        if (!objectManager.TryGetObject(itemObjectData.ItemObjectId, out ItemObject itemObject))
            return false;

        ItemModifier itemModifier = null;
        if (!itemObjectData.ItemModifierNull &&
            !objectManager.TryGetObject(itemObjectData.ItemModifierId, out itemModifier))
            return false;

        equipmentElement = new EquipmentElement(itemObject, itemModifier);
        return true;
    }

    private static void OpenBarter(PartyBase myParty, PartyBase otherParty)
    {
        var myHero = myParty?.LeaderHero;
        var otherHero = otherParty?.LeaderHero;
        if (myHero == null || otherHero == null) return;

        var barterData = new BarterData(myHero, otherHero, myParty, otherParty, null, 0, false);
        AddBarterGroups(barterData);
        AddPartyBarterables(barterData, myHero, otherHero, myParty, otherParty);
        AddPartyBarterables(barterData, otherHero, myHero, otherParty, myParty);

        BarterManager.Instance.BeginPlayerBarter(barterData);
    }

    private static void AddBarterGroups(BarterData barterData)
    {
        barterData.AddBarterGroup(new FiefBarterGroup());
        barterData.AddBarterGroup(new PrisonerBarterGroup());
        barterData.AddBarterGroup(new ItemBarterGroup());
        barterData.AddBarterGroup(new OtherBarterGroup());
        barterData.AddBarterGroup(new GoldBarterGroup());
    }

    private static void AddPartyBarterables(
        BarterData barterData,
        Hero ownerHero,
        Hero otherHero,
        PartyBase ownerParty,
        PartyBase otherParty)
    {
        barterData.AddBarterable<GoldBarterGroup>(new GoldBarterable(
            ownerHero,
            otherHero,
            ownerParty,
            otherParty,
            Math.Max(0, ownerHero.Gold)), false);

        AddPartyPeaceBarterable(barterData, ownerHero, otherHero, ownerParty, otherParty);
        AddPartyFiefBarterables(barterData, ownerHero, otherHero);
        AddPartyPrisonerBarterables(barterData, ownerHero, otherHero, ownerParty, otherParty);
        AddPartyItemBarterables(barterData, ownerHero, otherHero, ownerParty, otherParty);
        AddPartyTroopBarterables(barterData, ownerHero, otherHero, ownerParty, otherParty);
    }

    private static void AddPartyPeaceBarterable(
        BarterData barterData,
        Hero ownerHero,
        Hero otherHero,
        PartyBase ownerParty,
        PartyBase otherParty)
    {
        if (!PlayerPartyPeaceBarterable.CanOfferPeace(ownerParty, otherParty)) return;

        barterData.AddBarterable<OtherBarterGroup>(
            new PlayerPartyPeaceBarterable(ownerHero, otherHero, ownerParty, otherParty),
            false);
    }

    private static void AddPartyFiefBarterables(BarterData barterData, Hero ownerHero, Hero otherHero)
    {
        if (ownerHero?.Clan == null || otherHero == null) return;

        foreach (var fief in GetAllFiefs())
        {
            if (fief?.OwnerClan?.Leader != ownerHero) continue;
            if (fief.Settlement == null) continue;

            barterData.AddBarterable<FiefBarterGroup>(
                new FiefBarterable(fief.Settlement, ownerHero, otherHero),
                false);
        }
    }

    private static IEnumerable<Town> GetAllFiefs()
    {
        try
        {
            return Town.AllFiefs ?? Enumerable.Empty<Town>();
        }
        catch (NullReferenceException)
        {
            return Enumerable.Empty<Town>();
        }
    }

    private static void AddPartyPrisonerBarterables(
        BarterData barterData,
        Hero ownerHero,
        Hero otherHero,
        PartyBase ownerParty,
        PartyBase otherParty)
    {
        if (ownerParty == null) return;

        foreach (var prisoner in GetPrisonerHeroes(ownerParty))
        {
            if (prisoner?.HeroObject == null) continue;

            barterData.AddBarterable<PrisonerBarterGroup>(
                new TransferPrisonerBarterable(prisoner.HeroObject, ownerHero, ownerParty, otherHero, otherParty),
                false);
        }
    }

    private static IEnumerable<CharacterObject> GetPrisonerHeroes(PartyBase party)
    {
        try
        {
            return party.PrisonerHeroes ?? Enumerable.Empty<CharacterObject>();
        }
        catch (NullReferenceException)
        {
            return Enumerable.Empty<CharacterObject>();
        }
    }

    private static void AddPartyItemBarterables(
        BarterData barterData,
        Hero ownerHero,
        Hero otherHero,
        PartyBase ownerParty,
        PartyBase otherParty)
    {
        if (ownerParty?.ItemRoster == null) return;

        foreach (var item in ownerParty.ItemRoster)
        {
            if (item.Amount <= 0 || item.EquipmentElement.Item == null) continue;

            barterData.AddBarterable<ItemBarterGroup>(new ItemBarterable(
                ownerHero,
                otherHero,
                ownerParty,
                otherParty,
                item,
                Math.Max(0, item.EquipmentElement.Item.Value)), false);
        }
    }

    private static void AddPartyTroopBarterables(
        BarterData barterData,
        Hero ownerHero,
        Hero otherHero,
        PartyBase ownerParty,
        PartyBase otherParty)
    {
        if (ownerParty?.MemberRoster == null) return;

        foreach (var troop in ownerParty.MemberRoster.GetTroopRoster())
        {
            if (troop.Number <= 0 || troop.Character == null) continue;
            if (troop.Character == ownerHero?.CharacterObject) continue;

            barterData.AddBarterable<OtherBarterGroup>(
                new PlayerPartyTroopBarterable(ownerHero, otherHero, ownerParty, otherParty, troop),
                false);
        }
    }

    private ItemRosterElementData[] ResolveItemIds(IEnumerable<(ItemRosterElement, int)> items)
    {
        var result = new List<ItemRosterElementData>();

        foreach (var (item, count) in items)
        {
            if (!objectManager.TryGetId(item.EquipmentElement.Item, out var itemObjectId))
                continue;

            string itemModifierId = null;
            if (item.EquipmentElement.ItemModifier != null &&
                !objectManager.TryGetId(item.EquipmentElement.ItemModifier, out itemModifierId))
                continue;

            result.Add(new ItemRosterElementData(
                new ItemObjectData(itemObjectId, itemModifierId, item.EquipmentElement.ItemModifier == null),
                count));
        }

        return result.ToArray();
    }

    private ItemRosterElementData[] ResolvePartyItemIds(string partyId)
    {
        if (!objectManager.TryGetObject<PartyBase>(partyId, out var party))
            return Array.Empty<ItemRosterElementData>();

        return ResolveItemIds(GetItemRosterElements(party.ItemRoster));
    }

    private string[] ResolveSettlementIds(IEnumerable<Settlement> settlements)
    {
        var result = new List<string>();

        foreach (var settlement in settlements)
        {
            if (settlement == null || string.IsNullOrEmpty(settlement.StringId))
                continue;

            result.Add(settlement.StringId);
        }

        return result.ToArray();
    }

    private TroopRosterElementData[] ResolveCharacterIds(IEnumerable<(CharacterObject, int)> characters)
    {
        var result = new List<TroopRosterElementData>();

        foreach (var (character, count) in characters)
        {
            if (character == null || count <= 0) continue;

            if (!objectManager.TryGetIdWithLogging(character, out var characterId)) continue;

            result.Add(new TroopRosterElementData(characterId, count, 0, 0));
        }

        return result.ToArray();
    }

    private TroopRosterElementData[] ResolveTroopIds(IEnumerable<(TroopRosterElement, int)> troops)
    {
        var result = new List<TroopRosterElementData>();

        foreach (var (troop, count) in troops)
        {
            var character = troop.Character;
            if (character == null || count <= 0) continue;

            if (!objectManager.TryGetIdWithLogging(character, out var characterId))
                continue;

            result.Add(new TroopRosterElementData(characterId, count, troop.WoundedNumber, troop.Xp));
        }

        return result.ToArray();
    }

    private static IEnumerable<(ItemRosterElement, int)> GetItemRosterElements(ItemRoster itemRoster)
    {
        if (itemRoster == null) yield break;

        foreach (var item in itemRoster)
        {
            if (item.Amount <= 0) continue;

            yield return (item, item.Amount);
        }
    }

    private static IEnumerable<(ItemRosterElement, int)> GetOfferedBarterItems(BarterVM barterVM)
    {
        var result = new List<(ItemRosterElement, int)>();
        if (barterVM == null) return result;

        AddOfferedBarterItems(result, barterVM.LeftOfferList);
        AddOfferedBarterItems(result, barterVM.RightOfferList);

        return result;
    }

    private static int GetOfferedGold(BarterVM barterVM)
    {
        if (barterVM == null) return 0;

        return GetOfferedGold(barterVM.LeftOfferList) + GetOfferedGold(barterVM.RightOfferList);
    }

    private static int GetOfferedGold(IEnumerable<BarterItemVM> offeredItems)
    {
        if (offeredItems == null) return 0;

        var result = 0;
        foreach (var offeredItem in offeredItems)
        {
            if (!PlayerPartyTradeContext.CanOffer(offeredItem.Barterable)) continue;
            if (!(offeredItem.Barterable is GoldBarterable)) continue;

            result += Math.Max(0, offeredItem.Barterable.CurrentAmount);
        }

        return result;
    }

    private static bool HasOfferedPeace(BarterVM barterVM)
    {
        if (barterVM == null) return false;

        return HasOfferedPeace(barterVM.LeftOfferList) ||
               HasOfferedPeace(barterVM.RightOfferList);
    }

    private static bool HasOfferedPeace(IEnumerable<BarterItemVM> offeredItems)
    {
        if (offeredItems == null) return false;

        foreach (var offeredItem in offeredItems)
        {
            if (!PlayerPartyTradeContext.CanOffer(offeredItem.Barterable)) continue;
            if (offeredItem.Barterable is PlayerPartyPeaceBarterable) return true;
        }

        return false;
    }

    private static IEnumerable<Settlement> GetOfferedBarterFiefs(BarterVM barterVM)
    {
        var result = new List<Settlement>();
        if (barterVM == null) return result;

        AddOfferedBarterFiefs(result, barterVM.LeftOfferList);
        AddOfferedBarterFiefs(result, barterVM.RightOfferList);

        return result;
    }

    private static IEnumerable<(CharacterObject, int)> GetOfferedBarterPrisoners(BarterVM barterVM)
    {
        var result = new List<(CharacterObject, int)>();
        if (barterVM == null) return result;

        AddOfferedBarterPrisoners(result, barterVM.LeftOfferList);
        AddOfferedBarterPrisoners(result, barterVM.RightOfferList);

        return result;
    }

    private static IEnumerable<(TroopRosterElement, int)> GetOfferedBarterTroops(BarterVM barterVM)
    {
        var result = new List<(TroopRosterElement, int)>();
        if (barterVM == null) return result;

        AddOfferedBarterTroops(result, barterVM.LeftOfferList);
        AddOfferedBarterTroops(result, barterVM.RightOfferList);

        return result;
    }

    private static void AddOfferedBarterItems(List<(ItemRosterElement, int)> result, IEnumerable<BarterItemVM> offeredItems)
    {
        if (offeredItems == null) return;

        foreach (var offeredItem in offeredItems)
        {
            if (!PlayerPartyTradeContext.CanOffer(offeredItem.Barterable)) continue;
            if (!(offeredItem.Barterable is ItemBarterable itemBarterable)) continue;

            var count = Math.Min(offeredItem.Barterable.CurrentAmount, itemBarterable.ItemRosterElement.Amount);
            if (count <= 0) continue;

            result.Add((itemBarterable.ItemRosterElement, count));
        }
    }

    private static void AddOfferedBarterFiefs(List<Settlement> result, IEnumerable<BarterItemVM> offeredItems)
    {
        if (offeredItems == null) return;

        foreach (var offeredItem in offeredItems)
        {
            if (!PlayerPartyTradeContext.CanOffer(offeredItem.Barterable)) continue;
            if (!(offeredItem.Barterable is FiefBarterable fiefBarterable)) continue;

            result.Add(fiefBarterable.TargetSettlement);
        }
    }

    private static void AddOfferedBarterPrisoners(List<(CharacterObject, int)> result, IEnumerable<BarterItemVM> offeredItems)
    {
        if (offeredItems == null) return;

        foreach (var offeredItem in offeredItems)
        {
            if (!PlayerPartyTradeContext.CanOffer(offeredItem.Barterable)) continue;
            if (!(offeredItem.Barterable is TransferPrisonerBarterable transferPrisonerBarterable)) continue;
            if (!(PrisonerCharacterField?.GetValue(transferPrisonerBarterable) is Hero prisonerHero)) continue;

            result.Add((prisonerHero.CharacterObject, 1));
        }
    }

    private static void AddOfferedBarterTroops(List<(TroopRosterElement, int)> result, IEnumerable<BarterItemVM> offeredItems)
    {
        if (offeredItems == null) return;

        foreach (var offeredItem in offeredItems)
        {
            if (!PlayerPartyTradeContext.CanOffer(offeredItem.Barterable)) continue;
            if (!(offeredItem.Barterable is PlayerPartyTroopBarterable troopBarterable)) continue;

            var count = Math.Min(offeredItem.Barterable.CurrentAmount, troopBarterable.TroopRosterElement.Number);
            if (count <= 0) continue;

            result.Add((troopBarterable.TroopRosterElement, count));
        }
    }
}
