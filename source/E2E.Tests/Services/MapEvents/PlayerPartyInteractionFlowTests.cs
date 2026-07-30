using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Client.Services.SiegeEvents.Messages;
using Coop.Core.Server.Services.SiegeEvents.Messages;
using Coop.Core.Server.Services.Stances.Messages;
using Common.Messaging;
using Common.Network;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Barters.Messages;
using GameInterface.CoopSessionData;
using GameInterface.Services.Bandits.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.Inventory.Data;
using GameInterface.Services.Locations.Conversations;
using GameInterface.Services.Locations.Messages.Conversation;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Initialization;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.MapEvents.Patches;
using GameInterface.Services.MapEvents.PlayerPartyInteractions;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Interfaces;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.MobilePartyAIs.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyComponents.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Stances.Messages;
using GameInterface.Services.Villages.Interfaces;
using GameInterface.Services.TroopRosters.Data;
using HarmonyLib;
using Helpers;
using LiteNetLib;
using Missions.Messages;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CampaignBehaviors.BarterBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

public class PlayerPartyInteractionFlowTests : MapEventTestBase
{
    public PlayerPartyInteractionFlowTests(ITestOutputHelper output) : base(output)
    {
        ClearPlayerPartyInteractionState();
    }

    [Fact]
    public void ClientRequest_PlayerPartyInteraction_StartsServerDrivenDialogStates()
    {
        var (client1, _, initiatorPartyId, responderPartyId) = CreateTwoPlayerParties();

        RequestInteraction(client1, initiatorPartyId, responderPartyId);

        var started = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionStarted>().Single();
        Assert.Equal(initiatorPartyId, started.InitiatorPartyId);
        Assert.Equal(responderPartyId, started.ResponderPartyId);

        var states = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionState>().ToArray();
        Assert.Contains(states, s =>
            s.SessionId == started.SessionId &&
            s.PartyId == initiatorPartyId &&
            s.Phase == PlayerPartyInteractionPhase.InitialOptions &&
            s.IsInitiator &&
            s.Options.Contains(PlayerPartyInteractionOption.TradeProposal));
        Assert.Contains(states, s =>
            s.SessionId == started.SessionId &&
            s.PartyId == responderPartyId &&
            s.Phase == PlayerPartyInteractionPhase.WaitingForProposal &&
            !s.IsInitiator &&
            s.Options.SequenceEqual(new[] { PlayerPartyInteractionOption.Leave }));
    }

    [Fact]
    public void ClientRequest_InactivePlayerParty_DeniesWithoutStartingDialog()
    {
        var (client1, _, initiatorPartyId, responderPartyId) = CreateTwoPlayerParties();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(responderPartyId, out var responderParty));
            responderParty.MobileParty.IsActive = false;
        });
        Server.NetworkSentMessages.Clear();

        RequestInteraction(client1, initiatorPartyId, responderPartyId);

        var denied = Server.NetworkSentMessages.GetMessages<NetworkConversationDenied>().Single();
        Assert.Equal(ConversationDeniedReason.PlayerUnavailable, denied.Reason);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionStarted>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionState>());
    }

    [Fact]
    public void ClientRequest_BesiegingPlayer_DeniesWithoutStartingDialog()
    {
        var (client1, _, initiatorPartyId, responderPartyId) = CreateTwoPlayerParties();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(responderPartyId, out var responderParty));
            responderParty.MobileParty._besiegerCamp = ObjectHelper.SkipConstructor<BesiegerCamp>();
        });
        Server.NetworkSentMessages.Clear();

        RequestInteraction(client1, initiatorPartyId, responderPartyId);

        var denied = Server.NetworkSentMessages.GetMessages<NetworkConversationDenied>().Single();
        Assert.Equal(ConversationDeniedReason.PlayerUnavailable, denied.Reason);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionStarted>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionState>());
    }

    [Fact]
    public void ClientRequest_PlayerDefendingBesiegedSettlement_DeniesWithoutStartingDialog()
    {
        var (client1, _, initiatorPartyId, responderPartyId) = CreateTwoPlayerParties();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(initiatorPartyId, out var initiatorParty));
            var settlement = ObjectHelper.SkipConstructor<Settlement>();
            settlement.SiegeEvent = ObjectHelper.SkipConstructor<SiegeEvent>();
            initiatorParty.MobileParty._currentSettlement = settlement;
        });
        Server.NetworkSentMessages.Clear();

        RequestInteraction(client1, initiatorPartyId, responderPartyId);

        var denied = Server.NetworkSentMessages.GetMessages<NetworkConversationDenied>().Single();
        Assert.Equal(ConversationDeniedReason.PlayerUnavailable, denied.Reason);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionStarted>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionState>());
    }

    [Fact]
    public void OppositeDirectionInteractionRequest_ForReservedPair_IsIdempotent()
    {
        var (client1, client2, initiatorPartyId, responderPartyId) = CreateTwoPlayerParties();

        RequestInteraction(client1, initiatorPartyId, responderPartyId);
        RequestInteraction(client2, responderPartyId, initiatorPartyId);

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionStarted>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionDenied>());
    }

    [Fact]
    public void TradeProposal_AcceptedByResponder_EntersTradeActiveForBothParties()
    {
        var (client1, client2, initiatorPartyId, responderPartyId) = CreateTwoPlayerParties();
        RequestInteraction(client1, initiatorPartyId, responderPartyId);
        var sessionId = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionStarted>().Single().SessionId;
        var initiatorInitialState = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionState>().Single(s =>
            s.SessionId == sessionId &&
            s.PartyId == initiatorPartyId &&
            s.Phase == PlayerPartyInteractionPhase.InitialOptions);

        Server.NetworkSentMessages.Clear();
        client1.NetworkSentMessages.Clear();
        SubmitDialogOption(client1, initiatorInitialState, PlayerPartyInteractionOption.TradeProposal);

        var submittedOption = client1.NetworkSentMessages.GetMessages<NetworkSubmitPlayerPartyInteractionOption>().Single();
        Assert.Equal(sessionId, submittedOption.SessionId);
        Assert.Equal(initiatorPartyId, submittedOption.PartyId);
        Assert.Equal(PlayerPartyInteractionOption.TradeProposal, submittedOption.Option);

        var proposalStates = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionState>().ToArray();
        Assert.Contains(proposalStates, s =>
            s.PartyId == initiatorPartyId &&
            s.Phase == PlayerPartyInteractionPhase.WaitingForResponse &&
            s.Proposal == PlayerPartyInteractionProposal.Trade);
        Assert.Contains(proposalStates, s =>
            s.PartyId == responderPartyId &&
            s.Phase == PlayerPartyInteractionPhase.ProposalPending &&
            s.Proposal == PlayerPartyInteractionProposal.Trade &&
            s.Options.Contains(PlayerPartyInteractionOption.AcceptProposal));
        client2.Call(() =>
        {
            Assert.Equal(sessionId, PlayerPartyInteractionDialogState.SessionId);
            Assert.Equal(responderPartyId, PlayerPartyInteractionDialogState.PartyId);
            Assert.Equal(PlayerPartyInteractionPhase.ProposalPending, PlayerPartyInteractionDialogState.Phase);
            Assert.Equal(PlayerPartyInteractionProposal.Trade, PlayerPartyInteractionDialogState.Proposal);
            Assert.Equal("I have a proposal that may benefit us both.", PlayerPartyInteractionDialogState.GetDialogText());
            Assert.True(PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.AcceptProposal));
            Assert.True(PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.DeclineProposal));
            Assert.True(PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.Leave));
        });

        Server.NetworkSentMessages.Clear();
        SubmitOption(client2, sessionId, responderPartyId, PlayerPartyInteractionOption.AcceptProposal);

        var tradeStates = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionState>().ToArray();
        Assert.Contains(tradeStates, s => s.PartyId == initiatorPartyId && s.Phase == PlayerPartyInteractionPhase.TradeActive);
        Assert.Contains(tradeStates, s => s.PartyId == responderPartyId && s.Phase == PlayerPartyInteractionPhase.TradeActive);
    }

    [Fact]
    public void OptionSubmit_SpoofedResponderPartyId_DoesNotActAsResponder()
    {
        var (client1, _, initiatorPartyId, responderPartyId) = CreateTwoPlayerParties();
        RequestInteraction(client1, initiatorPartyId, responderPartyId);
        var sessionId = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionStarted>().Single().SessionId;
        SubmitOption(client1, sessionId, initiatorPartyId, PlayerPartyInteractionOption.TradeProposal);

        Server.NetworkSentMessages.Clear();
        client1.Call(() => client1.Resolve<INetwork>().SendAll(new NetworkSubmitPlayerPartyInteractionOption(
            sessionId,
            PlayerPartyInteractionOption.AcceptProposal,
            responderPartyId)));

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionState>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionEnded>());
    }

    [Fact]
    public void OfferServices_WithNoEnabledServiceOptions_ShowsDisabledOptionsAndCanEndInteraction()
    {
        var (client1, _, initiatorPartyId, responderPartyId) = CreateTwoPlayerParties();
        var responderClanLeaderId = TestEnvironment.CreateRegisteredObject<Hero>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(responderPartyId, out var responderParty));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(responderClanLeaderId, out var responderClanLeader));

            responderClanLeader.Clan = responderParty.LeaderHero.Clan;
            responderParty.LeaderHero.Clan.SetLeader(responderClanLeader);
        });

        RequestInteraction(client1, initiatorPartyId, responderPartyId);
        var sessionId = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionStarted>().Single().SessionId;
        var initialState = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionState>().Single(s =>
            s.SessionId == sessionId &&
            s.PartyId == initiatorPartyId &&
            s.Phase == PlayerPartyInteractionPhase.InitialOptions);

        Assert.Contains(PlayerPartyInteractionOption.OfferServices, initialState.Options);
        Assert.Contains(PlayerPartyInteractionOption.JoinClan, initialState.Options);
        Assert.Contains(PlayerPartyInteractionOption.Vassal, initialState.Options);
        Assert.DoesNotContain(PlayerPartyInteractionOption.JoinClan, initialState.EnabledOptions);
        Assert.DoesNotContain(PlayerPartyInteractionOption.Vassal, initialState.EnabledOptions);
        Assert.Equal(PlayerPartyInteractionVassalUnavailableReason.TargetIsNotKingdomLeader, initialState.VassalUnavailableReason);

        Server.NetworkSentMessages.Clear();
        client1.NetworkSentMessages.Clear();
        OpenServiceOptions(client1, initialState);

        Assert.Empty(client1.NetworkSentMessages.GetMessages<NetworkSubmitPlayerPartyInteractionOption>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionState>());
        client1.Call(() =>
        {
            Assert.Equal(PlayerPartyInteractionPhase.OfferServices, PlayerPartyInteractionDialogState.Phase);
            Assert.True(PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.Leave));
            Assert.True(PlayerPartyInteractionDialogState.IsOptionEnabled(PlayerPartyInteractionOption.Leave));
            Assert.True(PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.JoinClan));
            Assert.False(PlayerPartyInteractionDialogState.IsOptionEnabled(PlayerPartyInteractionOption.JoinClan));
            Assert.True(PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.Vassal));
            Assert.False(PlayerPartyInteractionDialogState.IsOptionEnabled(PlayerPartyInteractionOption.Vassal));
        });

        Server.NetworkSentMessages.Clear();
        SubmitCurrentDialogOption(client1, PlayerPartyInteractionOption.Leave);

        var ended = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionEnded>().Single();
        Assert.Equal(PlayerPartyInteractionOutcomeType.Left, ended.OutcomeType);
    }

    [Fact]
    public void OfferServices_WithClanLeader_ShowsJoinClanDisabledAndNevermind()
    {
        var (client1, _, initiatorPartyId, responderPartyId) = CreateTwoPlayerParties();

        RequestInteraction(client1, initiatorPartyId, responderPartyId);
        var sessionId = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionStarted>().Single().SessionId;
        var initialState = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionState>().Single(s =>
            s.SessionId == sessionId &&
            s.PartyId == initiatorPartyId &&
            s.Phase == PlayerPartyInteractionPhase.InitialOptions);

        Server.NetworkSentMessages.Clear();
        client1.NetworkSentMessages.Clear();
        OpenServiceOptions(client1, initialState);

        Assert.Empty(client1.NetworkSentMessages.GetMessages<NetworkSubmitPlayerPartyInteractionOption>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionState>());
        client1.Call(() =>
        {
            Assert.Equal(PlayerPartyInteractionPhase.OfferServices, PlayerPartyInteractionDialogState.Phase);
            Assert.True(PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.JoinClan));
            Assert.False(PlayerPartyInteractionDialogState.IsOptionEnabled(PlayerPartyInteractionOption.JoinClan));
            Assert.True(PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.Vassal));
            Assert.False(PlayerPartyInteractionDialogState.IsOptionEnabled(PlayerPartyInteractionOption.Vassal));
            Assert.True(PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.Leave));
            Assert.True(PlayerPartyInteractionDialogState.IsOptionEnabled(PlayerPartyInteractionOption.Leave));
        });
    }

    [Fact]
    public void OfferServices_WithTierOneClanAndKingdomLeader_DisablesVassal()
    {
        var (client1, _, initiatorPartyId, responderPartyId) = CreateTwoPlayerParties();
        SetupResponderKingdomLeader(initiatorPartyId, responderPartyId, initiatorClanTier: 1);

        RequestInteraction(client1, initiatorPartyId, responderPartyId);
        var sessionId = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionStarted>().Single().SessionId;
        var initialState = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionState>().Single(s =>
            s.SessionId == sessionId &&
            s.PartyId == initiatorPartyId &&
            s.Phase == PlayerPartyInteractionPhase.InitialOptions);
        Assert.Equal(PlayerPartyInteractionVassalUnavailableReason.InitiatorClanTierTooLow, initialState.VassalUnavailableReason);

        OpenServiceOptions(client1, initialState);

        client1.Call(() =>
        {
            Assert.True(PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.JoinClan));
            Assert.False(PlayerPartyInteractionDialogState.IsOptionEnabled(PlayerPartyInteractionOption.JoinClan));
            Assert.True(PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.Leave));
            Assert.True(PlayerPartyInteractionDialogState.IsOptionEnabled(PlayerPartyInteractionOption.Leave));
            Assert.True(PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.Vassal));
            Assert.False(PlayerPartyInteractionDialogState.IsOptionEnabled(PlayerPartyInteractionOption.Vassal, out var explanation));
            Assert.Equal("Your clan must be at least tier 2 to swear allegiance.", explanation.ToString());
        });
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void OfferServices_WithEligibleClanAndKingdomLeader_EnablesVassal(int initiatorClanTier)
    {
        var (client1, _, initiatorPartyId, responderPartyId) = CreateTwoPlayerParties();
        SetupResponderKingdomLeader(initiatorPartyId, responderPartyId, initiatorClanTier);

        RequestInteraction(client1, initiatorPartyId, responderPartyId);
        var sessionId = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionStarted>().Single().SessionId;
        var initialState = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionState>().Single(s =>
            s.SessionId == sessionId &&
            s.PartyId == initiatorPartyId &&
            s.Phase == PlayerPartyInteractionPhase.InitialOptions);

        Assert.Contains(PlayerPartyInteractionOption.Vassal, initialState.Options);
        Assert.Contains(PlayerPartyInteractionOption.Vassal, initialState.EnabledOptions);

        OpenServiceOptions(client1, initialState);

        client1.Call(() =>
        {
            Assert.True(PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.JoinClan));
            Assert.False(PlayerPartyInteractionDialogState.IsOptionEnabled(PlayerPartyInteractionOption.JoinClan));
            Assert.True(PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.Leave));
            Assert.True(PlayerPartyInteractionDialogState.IsOptionEnabled(PlayerPartyInteractionOption.Leave));
            Assert.True(PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.Vassal));
            Assert.True(PlayerPartyInteractionDialogState.IsOptionEnabled(PlayerPartyInteractionOption.Vassal));
        });
    }

    [Fact]
    public void ClanServiceProposal_Disabled_DoesNotSubmitOrJoinResponderClan()
    {
        var (client1, _, initiatorPartyId, responderPartyId) = CreateTwoPlayerParties();

        RequestInteraction(client1, initiatorPartyId, responderPartyId);
        var sessionId = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionStarted>().Single().SessionId;
        var initialState = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionState>().Single(s =>
            s.SessionId == sessionId &&
            s.PartyId == initiatorPartyId &&
            s.Phase == PlayerPartyInteractionPhase.InitialOptions);

        OpenServiceOptions(client1, initialState);

        Server.NetworkSentMessages.Clear();
        client1.NetworkSentMessages.Clear();
        SubmitCurrentDialogOption(client1, PlayerPartyInteractionOption.JoinClan);

        Assert.Empty(client1.NetworkSentMessages.GetMessages<NetworkSubmitPlayerPartyInteractionOption>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionState>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionEnded>());

        SubmitOption(client1, sessionId, initiatorPartyId, PlayerPartyInteractionOption.JoinClan);

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionState>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionEnded>());
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(initiatorPartyId, out var initiatorParty));
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(responderPartyId, out var responderParty));

            Assert.NotEqual(responderParty.LeaderHero.Clan, initiatorParty.LeaderHero.Clan);
            Assert.NotEqual(responderParty.LeaderHero.Clan, initiatorParty.MobileParty.ActualClan);
        });
    }

    [Fact]
    public void VassalServiceProposal_AcceptedByKingdomLeader_JoinsKingdomOnAllInstances()
    {
        var (client1, client2, initiatorPartyId, responderPartyId) = CreateTwoPlayerParties();
        SetupResponderKingdomLeader(initiatorPartyId, responderPartyId, initiatorClanTier: 2);

        RequestInteraction(client1, initiatorPartyId, responderPartyId);
        var sessionId = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionStarted>().Single().SessionId;
        var initialState = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionState>().Single(s =>
            s.SessionId == sessionId &&
            s.PartyId == initiatorPartyId &&
            s.Phase == PlayerPartyInteractionPhase.InitialOptions);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(initiatorPartyId, out var initiatorParty));
            Assert.Null(initiatorParty.LeaderHero.Clan.Kingdom);
        });

        OpenServiceOptions(client1, initialState);
        SubmitCurrentDialogOption(client1, PlayerPartyInteractionOption.Vassal);
        Server.NetworkSentMessages.Clear();
        SubmitOption(client2, sessionId, responderPartyId, PlayerPartyInteractionOption.AcceptProposal);

        var ended = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionEnded>().Single();
        Assert.Equal(PlayerPartyInteractionOutcomeType.VassalAccepted, ended.OutcomeType);

        foreach (var instance in new[] { Server, client1, client2 })
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<PartyBase>(initiatorPartyId, out var initiatorParty));
                Assert.True(instance.ObjectManager.TryGetObject<PartyBase>(responderPartyId, out var responderParty));

                var initiatorClan = initiatorParty.LeaderHero.Clan;
                var responderKingdom = responderParty.LeaderHero.Clan.Kingdom;

                Assert.Same(responderKingdom, initiatorClan.Kingdom);
                Assert.Contains(initiatorClan, responderKingdom.Clans);
                Assert.False(initiatorClan.IsUnderMercenaryService);
            });
        }
    }

    [Theory]
    [InlineData(PlayerPartyInteractionProposal.Trade, "I have a proposal that may benefit us both.")]
    [InlineData(PlayerPartyInteractionProposal.JoinClan, "(COMING SOON) I wish to offer my services in your clan.")]
    [InlineData(PlayerPartyInteractionProposal.Vassal, "I wish to swear my allegiance to your majesty.")]
    public void ProposalPending_DialogText_ShowsInitiatorSelectedLine(
        PlayerPartyInteractionProposal proposal,
        string expectedText)
    {
        PlayerPartyInteractionDialogState.Apply(new NetworkPlayerPartyInteractionState(
            "session-1",
            "responder-party",
            "initiator-party",
            "RandomPlayer",
            PlayerPartyInteractionPhase.ProposalPending,
            proposal,
            new[]
            {
                PlayerPartyInteractionOption.AcceptProposal,
                PlayerPartyInteractionOption.DeclineProposal,
                PlayerPartyInteractionOption.Leave
            },
            isInitiator: false));

        try
        {
            Assert.Equal(expectedText, PlayerPartyInteractionDialogState.GetDialogText());
        }
        finally
        {
            PlayerPartyInteractionDialogState.Clear("session-1");
        }
    }

    [Theory]
    [InlineData(PlayerPartyInteractionOutcomeType.TradeAccepted, "Barter offer accepted.")]
    [InlineData(PlayerPartyInteractionOutcomeType.TradeDeclined, "Trade proposal declined.")]
    [InlineData(PlayerPartyInteractionOutcomeType.ClanJoinAccepted, "Clan service proposal accepted.")]
    [InlineData(PlayerPartyInteractionOutcomeType.ClanJoinDeclined, "Clan service proposal declined.")]
    [InlineData(PlayerPartyInteractionOutcomeType.VassalAccepted, "Vassalage offer accepted.")]
    [InlineData(PlayerPartyInteractionOutcomeType.VassalDeclined, "Vassalage offer declined.")]
    public void OutcomeMessages_UsePlayerPartyInteractionResult(PlayerPartyInteractionOutcomeType outcomeType, string expectedMessage)
    {
        Assert.Equal(expectedMessage, PlayerPartyTradeContext.GetOutcomeMessage(outcomeType));
    }

    [Fact]
    public void RemovedMercenaryInteractionValues_AreNotDefined()
    {
        Assert.DoesNotContain("Mercenary", Enum.GetNames(typeof(PlayerPartyInteractionOption)));
        Assert.DoesNotContain("Mercenary", Enum.GetNames(typeof(PlayerPartyInteractionProposal)));
        Assert.DoesNotContain("MercenaryAccepted", Enum.GetNames(typeof(PlayerPartyInteractionOutcomeType)));
        Assert.DoesNotContain("MercenaryDeclined", Enum.GetNames(typeof(PlayerPartyInteractionOutcomeType)));
    }

    [Fact]
    public void TradeBarterData_IncludesPartyItemRosterBarterables()
    {
        var (_, _, initiatorPartyId, responderPartyId) = CreateTwoPlayerParties();
        var initiatorItemId = TestEnvironment.CreateRegisteredObject<ItemObject>();
        var responderItemId = TestEnvironment.CreateRegisteredObject<ItemObject>();
        var initiatorTroopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(initiatorPartyId, out var initiatorParty));
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(responderPartyId, out var responderParty));
            Assert.True(Server.ObjectManager.TryGetObject<ItemObject>(initiatorItemId, out var initiatorItem));
            Assert.True(Server.ObjectManager.TryGetObject<ItemObject>(responderItemId, out var responderItem));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(initiatorTroopId, out var initiatorTroop));

            initiatorParty.ItemRoster.AddToCounts(initiatorItem, 3);
            responderParty.ItemRoster.AddToCounts(responderItem, 4);
            initiatorParty.MemberRoster.AddToCounts(initiatorTroop, 5);

            var barterData = new BarterData(
                initiatorParty.LeaderHero,
                responderParty.LeaderHero,
                initiatorParty,
                responderParty,
                null,
                0,
                false);

            InvokeAddBarterGroups(barterData);
            InvokeAddPartyBarterables(barterData, initiatorParty.LeaderHero, responderParty.LeaderHero, initiatorParty, responderParty);
            InvokeAddPartyBarterables(barterData, responderParty.LeaderHero, initiatorParty.LeaderHero, responderParty, initiatorParty);

            var barterables = barterData.GetBarterables();

            Assert.Contains(barterables, b =>
                b is ItemBarterable itemBarterable &&
                itemBarterable.OriginalParty == initiatorParty &&
                itemBarterable.Group is ItemBarterGroup &&
                itemBarterable.ItemRosterElement.Amount == 3);
            Assert.Contains(barterables, b =>
                b is ItemBarterable itemBarterable &&
                itemBarterable.OriginalParty == responderParty &&
                itemBarterable.Group is ItemBarterGroup &&
                itemBarterable.ItemRosterElement.Amount == 4);
            var troopBarterable = Assert.Single(
                barterables.OfType<PlayerPartyTroopBarterable>(),
                b => b.OriginalParty == initiatorParty && b.TroopRosterElement.Character == initiatorTroop);
            Assert.Equal(initiatorParty, troopBarterable.OriginalParty);
            Assert.IsType<OtherBarterGroup>(troopBarterable.Group);
            Assert.Equal(initiatorTroop, troopBarterable.TroopRosterElement.Character);
            Assert.Equal(5, troopBarterable.TroopRosterElement.Number);
            Assert.Equal("item_barterable", troopBarterable.StringID);
            Assert.IsType<CharacterImageIdentifier>(troopBarterable.GetVisualIdentifier());
            Assert.Equal(2, barterables.OfType<GoldBarterable>().Count());
        });
    }

    [Fact]
    public void TradeContext_CanOffer_AllowsLocalFiefsWithNullOriginalParty()
    {
        var (_, _, initiatorPartyId, responderPartyId) = CreateTwoPlayerParties();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(initiatorPartyId, out var initiatorParty));
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(responderPartyId, out var responderParty));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            var localFief = new FiefBarterable(settlement, initiatorParty.LeaderHero, responderParty.LeaderHero);
            var remoteFief = new FiefBarterable(settlement, responderParty.LeaderHero, initiatorParty.LeaderHero);

            Assert.Null(localFief.OriginalParty);
            Assert.Null(remoteFief.OriginalParty);

            PlayerPartyTradeContext.Begin("session-1", initiatorParty);
            try
            {
                Assert.True(PlayerPartyTradeContext.CanOffer(localFief));
                Assert.False(PlayerPartyTradeContext.CanOffer(remoteFief));
            }
            finally
            {
                PlayerPartyTradeContext.End("session-1");
            }
        });
    }

    [Fact]
    public void TradeActiveState_IncludesServerPartyItemSnapshots()
    {
        var (client1, client2, initiatorPartyId, responderPartyId) = CreateTwoPlayerParties();
        var initiatorItemId = TestEnvironment.CreateRegisteredObject<ItemObject>();
        var responderItemId = TestEnvironment.CreateRegisteredObject<ItemObject>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(initiatorPartyId, out var initiatorParty));
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(responderPartyId, out var responderParty));
            Assert.True(Server.ObjectManager.TryGetObject<ItemObject>(initiatorItemId, out var initiatorItem));
            Assert.True(Server.ObjectManager.TryGetObject<ItemObject>(responderItemId, out var responderItem));

            initiatorParty.ItemRoster.AddToCounts(initiatorItem, 3);
            responderParty.ItemRoster.AddToCounts(responderItem, 4);
        });

        Server.NetworkSentMessages.Clear();
        StartTrade(client1, client2, initiatorPartyId, responderPartyId);

        var tradeStates = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionState>()
            .Where(s => s.Phase == PlayerPartyInteractionPhase.TradeActive)
            .ToArray();

        var initiatorState = tradeStates.Single(s => s.PartyId == initiatorPartyId);
        AssertPartyItemSnapshotContains(initiatorState.PartyItems, initiatorItemId, 3);
        AssertPartyItemSnapshotContains(initiatorState.OtherPartyItems, responderItemId, 4);

        var responderState = tradeStates.Single(s => s.PartyId == responderPartyId);
        AssertPartyItemSnapshotContains(responderState.PartyItems, responderItemId, 4);
        AssertPartyItemSnapshotContains(responderState.OtherPartyItems, initiatorItemId, 3);
    }

    [Fact]
    public void TradeAccept_FromBothParties_EndsWithTradeAcceptedOutcome()
    {
        var (client1, client2, initiatorPartyId, responderPartyId) = CreateTwoPlayerParties();
        var sessionId = StartTrade(client1, client2, initiatorPartyId, responderPartyId);

        Server.NetworkSentMessages.Clear();
        client1.Call(() => client1.Resolve<INetwork>().SendAll(new NetworkPlayerPartyTradeAcceptChanged(sessionId, accepted: true)));
        client2.Call(() => client2.Resolve<INetwork>().SendAll(new NetworkPlayerPartyTradeAcceptChanged(sessionId, accepted: true)));

        var ended = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionEnded>().Single();
        Assert.Equal(sessionId, ended.SessionId);
        Assert.Equal(initiatorPartyId, ended.InitiatorPartyId);
        Assert.Equal(responderPartyId, ended.ResponderPartyId);
        Assert.Equal(PlayerPartyInteractionOutcomeType.TradeAccepted, ended.OutcomeType);
        AssertInteractionStateCleared(client1);
        AssertInteractionStateCleared(client2);

        Server.NetworkSentMessages.Clear();
        RequestInteraction(client1, initiatorPartyId, responderPartyId);

        var restarted = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionStarted>().Single();
        Assert.NotEqual(sessionId, restarted.SessionId);
        Assert.Equal(initiatorPartyId, restarted.InitiatorPartyId);
        Assert.Equal(responderPartyId, restarted.ResponderPartyId);
    }

    [Fact]
    public void TradeAccept_FromBothParties_AppliesAcceptedTradeContents()
    {
        var (client1, client2, initiatorPartyId, responderPartyId) = CreateTwoPlayerParties();
        var initiatorItemId = TestEnvironment.CreateRegisteredObject<ItemObject>();
        var responderItemId = TestEnvironment.CreateRegisteredObject<ItemObject>();
        var initiatorTroopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        var responderTroopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        var initiatorPrisonerId = TestEnvironment.CreateRegisteredObject<Hero>();
        var responderPrisonerId = TestEnvironment.CreateRegisteredObject<Hero>();
        var initiatorSettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var responderSettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var initiatorTownId = TestEnvironment.CreateRegisteredObject<Town>();
        var responderTownId = TestEnvironment.CreateRegisteredObject<Town>();

        // Prisoners are Heroes, but the prison roster (and the trade resolution) works on the Hero's
        // unique CharacterObject. The offer must reference the prisoner by that CharacterObject's id,
        // not the Hero's id - resolved below.
        string initiatorPrisonerCharacterId = null;
        string responderPrisonerCharacterId = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(initiatorPartyId, out var initiatorParty));
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(responderPartyId, out var responderParty));
            Assert.True(Server.ObjectManager.TryGetObject<ItemObject>(initiatorItemId, out var initiatorItem));
            Assert.True(Server.ObjectManager.TryGetObject<ItemObject>(responderItemId, out var responderItem));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(initiatorTroopId, out var initiatorTroop));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(responderTroopId, out var responderTroop));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(initiatorPrisonerId, out var initiatorPrisoner));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(responderPrisonerId, out var responderPrisoner));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(initiatorSettlementId, out var initiatorSettlement));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(responderSettlementId, out var responderSettlement));
            Assert.True(Server.ObjectManager.TryGetObject<Town>(initiatorTownId, out var initiatorTown));
            Assert.True(Server.ObjectManager.TryGetObject<Town>(responderTownId, out var responderTown));

            initiatorParty.LeaderHero.Gold = 100;
            responderParty.LeaderHero.Gold = 60;
            initiatorParty.ItemRoster.AddToCounts(initiatorItem, 5);
            responderParty.ItemRoster.AddToCounts(responderItem, 7);
            initiatorParty.MemberRoster.AddToCounts(initiatorTroop, 6);
            responderParty.MemberRoster.AddToCounts(responderTroop, 8);
            initiatorParty.PrisonRoster.AddToCounts(initiatorPrisoner.CharacterObject, 1);
            responderParty.PrisonRoster.AddToCounts(responderPrisoner.CharacterObject, 1);
            // The CharacterObject is already registered (its own id); reference it by that id. Fall
            // back to registering it if not.
            if (!Server.ObjectManager.TryGetId(initiatorPrisoner.CharacterObject, out initiatorPrisonerCharacterId))
                Assert.True(Server.ObjectManager.AddExisting(initiatorPrisonerCharacterId = "InitiatorPrisonerCharacter", initiatorPrisoner.CharacterObject));
            if (!Server.ObjectManager.TryGetId(responderPrisoner.CharacterObject, out responderPrisonerCharacterId))
                Assert.True(Server.ObjectManager.AddExisting(responderPrisonerCharacterId = "ResponderPrisonerCharacter", responderPrisoner.CharacterObject));
            SetupFief(initiatorSettlement, initiatorTown, initiatorParty);
            SetupFief(responderSettlement, responderTown, responderParty);
        });

        var sessionId = StartTrade(client1, client2, initiatorPartyId, responderPartyId);

        client1.Call(() => client1.Resolve<INetwork>().SendAll(new NetworkPlayerPartyTradeOfferUpdated(
            sessionId,
            initiatorPartyId,
            new[]
            {
                new ItemRosterElementData(new ItemObjectData(initiatorItemId, null, itemModifierNull: true), 2)
            },
            new[] { new TroopRosterElementData(initiatorTroopId, 4, 0, 0) },
            offeredGold: 25,
            offeredFiefs: new[] { initiatorSettlementId },
            offeredPrisoners: new[] { new TroopRosterElementData(initiatorPrisonerCharacterId, 1, 0, 0) })));
        client2.Call(() => client2.Resolve<INetwork>().SendAll(new NetworkPlayerPartyTradeOfferUpdated(
            sessionId,
            responderPartyId,
            new[]
            {
                new ItemRosterElementData(new ItemObjectData(responderItemId, null, itemModifierNull: true), 3)
            },
            new[] { new TroopRosterElementData(responderTroopId, 5, 0, 0) },
            offeredGold: 10,
            offeredFiefs: new[] { responderSettlementId },
            offeredPrisoners: new[] { new TroopRosterElementData(responderPrisonerCharacterId, 1, 0, 0) })));

        Server.NetworkSentMessages.Clear();
        client1.Call(() => client1.Resolve<INetwork>().SendAll(new NetworkPlayerPartyTradeAcceptChanged(sessionId, accepted: true)));
        client2.Call(() => client2.Resolve<INetwork>().SendAll(new NetworkPlayerPartyTradeAcceptChanged(sessionId, accepted: true)));

        var ended = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionEnded>().Single();
        Assert.Equal(PlayerPartyInteractionOutcomeType.TradeAccepted, ended.OutcomeType);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(initiatorPartyId, out var initiatorParty));
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(responderPartyId, out var responderParty));
            Assert.True(Server.ObjectManager.TryGetObject<ItemObject>(initiatorItemId, out var initiatorItem));
            Assert.True(Server.ObjectManager.TryGetObject<ItemObject>(responderItemId, out var responderItem));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(initiatorTroopId, out var initiatorTroop));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(responderTroopId, out var responderTroop));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(initiatorPrisonerId, out var initiatorPrisoner));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(responderPrisonerId, out var responderPrisoner));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(initiatorSettlementId, out var initiatorSettlement));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(responderSettlementId, out var responderSettlement));

            Assert.Equal(85, initiatorParty.LeaderHero.Gold);
            Assert.Equal(75, responderParty.LeaderHero.Gold);

            Assert.Equal(3, GetItemAmount(initiatorParty, initiatorItem));
            Assert.Equal(2, GetItemAmount(responderParty, initiatorItem));
            Assert.Equal(3, GetItemAmount(initiatorParty, responderItem));
            Assert.Equal(4, GetItemAmount(responderParty, responderItem));

            Assert.Equal(2, initiatorParty.MemberRoster.GetElementNumber(initiatorTroop));
            Assert.Equal(4, responderParty.MemberRoster.GetElementNumber(initiatorTroop));
            Assert.Equal(5, initiatorParty.MemberRoster.GetElementNumber(responderTroop));
            Assert.Equal(3, responderParty.MemberRoster.GetElementNumber(responderTroop));

            Assert.Equal(0, initiatorParty.PrisonRoster.GetElementNumber(initiatorPrisoner.CharacterObject));
            Assert.Equal(1, responderParty.PrisonRoster.GetElementNumber(initiatorPrisoner.CharacterObject));
            Assert.Equal(1, initiatorParty.PrisonRoster.GetElementNumber(responderPrisoner.CharacterObject));
            Assert.Equal(0, responderParty.PrisonRoster.GetElementNumber(responderPrisoner.CharacterObject));

            Assert.Equal(responderParty.LeaderHero.Clan, initiatorSettlement.OwnerClan);
            Assert.Equal(initiatorParty.LeaderHero.Clan, responderSettlement.OwnerClan);
        });
    }

    [Fact]
    public void PeaceTrade_LeaderOffersPeaceFromBothSides_AcceptingTradeAppliesPeace()
    {
        var (client1, client2, initiatorPartyId, responderPartyId) = CreateTwoPlayerParties();
        var (initiatorClanId, responderClanId) = MakePartiesHostile(initiatorPartyId, responderPartyId);
        AssertPeaceBarterablesAvailable(initiatorPartyId, responderPartyId);

        var sessionId = StartTrade(client1, client2, initiatorPartyId, responderPartyId);

        Server.NetworkSentMessages.Clear();
        client1.Call(() => client1.Resolve<INetwork>().SendAll(new NetworkPlayerPartyTradeOfferUpdated(
            sessionId,
            initiatorPartyId,
            Array.Empty<ItemRosterElementData>(),
            Array.Empty<TroopRosterElementData>(),
            offeredPeace: true)));
        client2.Call(() => client2.Resolve<INetwork>().SendAll(new NetworkPlayerPartyTradeOfferUpdated(
            sessionId,
            responderPartyId,
            Array.Empty<ItemRosterElementData>(),
            Array.Empty<TroopRosterElementData>(),
            offeredPeace: true)));

        var peaceOffers = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyTradeOfferUpdated>()
            .Where(message => message.OfferedPeace)
            .ToArray();
        Assert.Contains(peaceOffers, offer => offer.PartyId == initiatorPartyId);
        Assert.Contains(peaceOffers, offer => offer.PartyId == responderPartyId);

        Server.NetworkSentMessages.Clear();
        client1.Call(() => client1.Resolve<INetwork>().SendAll(new NetworkPlayerPartyTradeAcceptChanged(sessionId, accepted: true)));
        client2.Call(() => client2.Resolve<INetwork>().SendAll(new NetworkPlayerPartyTradeAcceptChanged(sessionId, accepted: true)));

        var ended = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionEnded>().Single();
        Assert.Equal(PlayerPartyInteractionOutcomeType.TradeAccepted, ended.OutcomeType);

        var peaceMade = Server.NetworkSentMessages.GetMessages<NetworkMakePeace>().Single();
        Assert.Equal(initiatorClanId, peaceMade.Faction1Id);
        Assert.Equal(responderClanId, peaceMade.Faction2Id);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyHostileEncounterStarted>());

        AssertPeaceMade(Server, initiatorClanId, responderClanId);
        foreach (var syncedClient in Clients)
        {
            Assert.Contains(syncedClient.InternalMessages.GetMessages<MakePeaceChanged>(), message =>
                message.Faction1Id == initiatorClanId &&
                message.Faction2Id == responderClanId);
            AssertPeaceMade(syncedClient, initiatorClanId, responderClanId);
        }
    }

    [Fact]
    public void PeaceTrade_NonLeaderDoesNotExposeOrApplyPeace()
    {
        var (client1, client2, initiatorPartyId, responderPartyId) = CreateTwoPlayerParties();
        var (initiatorClanId, responderClanId) = MakePartiesHostile(initiatorPartyId, responderPartyId);
        ReplaceResponderClanLeader(responderPartyId);
        AssertPeaceBarterablesUnavailable(initiatorPartyId, responderPartyId);

        var sessionId = StartTrade(client1, client2, initiatorPartyId, responderPartyId);

        Server.NetworkSentMessages.Clear();
        client1.Call(() => client1.Resolve<INetwork>().SendAll(new NetworkPlayerPartyTradeOfferUpdated(
            sessionId,
            initiatorPartyId,
            Array.Empty<ItemRosterElementData>(),
            Array.Empty<TroopRosterElementData>(),
            offeredPeace: true)));

        var relayedOffer = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyTradeOfferUpdated>().Single();
        Assert.Equal(initiatorPartyId, relayedOffer.PartyId);
        Assert.False(relayedOffer.OfferedPeace);

        client2.Call(() => client2.Resolve<INetwork>().SendAll(new NetworkPlayerPartyTradeOfferUpdated(
            sessionId,
            responderPartyId,
            Array.Empty<ItemRosterElementData>(),
            Array.Empty<TroopRosterElementData>(),
            offeredPeace: true)));

        Server.NetworkSentMessages.Clear();
        client1.Call(() => client1.Resolve<INetwork>().SendAll(new NetworkPlayerPartyTradeAcceptChanged(sessionId, accepted: true)));
        client2.Call(() => client2.Resolve<INetwork>().SendAll(new NetworkPlayerPartyTradeAcceptChanged(sessionId, accepted: true)));

        var ended = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionEnded>().Single();
        Assert.Equal(PlayerPartyInteractionOutcomeType.TradeAccepted, ended.OutcomeType);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkMakePeace>());
        AssertWarDeclared(Server, initiatorClanId, responderClanId);
    }

    [Fact]
    public void LeaveOption_EndsInteractionAndClearsTracking()
    {
        var (client1, _, initiatorPartyId, responderPartyId) = CreateTwoPlayerParties();
        RequestInteraction(client1, initiatorPartyId, responderPartyId);
        var sessionId = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionStarted>().Single().SessionId;

        Server.NetworkSentMessages.Clear();
        SubmitOption(client1, sessionId, initiatorPartyId, PlayerPartyInteractionOption.Leave);

        var ended = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionEnded>().Single();
        Assert.Equal(PlayerPartyInteractionOutcomeType.Left, ended.OutcomeType);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionState>());
    }

    [Fact]
    public void ConversationRequest_WhilePlayerInteractionActive_IsIgnoredUntilInteractionEnds()
    {
        var (client1, _, initiatorPartyId, responderPartyId) = CreateTwoPlayerParties();
        RequestInteraction(client1, initiatorPartyId, responderPartyId);
        var sessionId = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionStarted>().Single().SessionId;

        client1.Call(() => Assert.True(PlayerPartyInteractionDialogState.HasActiveState));
        client1.NetworkSentMessages.Clear();

        PublishConversationRequest(client1, initiatorPartyId, responderPartyId);

        Assert.Empty(client1.NetworkSentMessages.GetMessages<NetworkRequestConversation>());

        SubmitOption(client1, sessionId, initiatorPartyId, PlayerPartyInteractionOption.Leave);
        AssertInteractionStateCleared(client1);
        client1.NetworkSentMessages.Clear();

        PublishConversationRequest(client1, initiatorPartyId, responderPartyId);

        Assert.Single(client1.NetworkSentMessages.GetMessages<NetworkRequestConversation>());
    }

    [Fact]
    public void EndedInteraction_IgnoresDelayedStateForSameSession()
    {
        var (client1, _, initiatorPartyId, responderPartyId) = CreateTwoPlayerParties();
        RequestInteraction(client1, initiatorPartyId, responderPartyId);
        var sessionId = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionStarted>().Single().SessionId;
        var initialState = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionState>()
            .Single(state => state.SessionId == sessionId && state.PartyId == initiatorPartyId);

        SubmitOption(client1, sessionId, initiatorPartyId, PlayerPartyInteractionOption.Leave);
        AssertInteractionStateCleared(client1);

        client1.SimulateMessage(Server.NetPeer, initialState);

        AssertInteractionStateCleared(client1);
    }

    [Fact]
    public void TradeProposal_DeclinedByResponder_EndsInteraction()
    {
        var (client1, client2, initiatorPartyId, responderPartyId) = CreateTwoPlayerParties();
        RequestInteraction(client1, initiatorPartyId, responderPartyId);
        var sessionId = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionStarted>().Single().SessionId;
        SubmitOption(client1, sessionId, initiatorPartyId, PlayerPartyInteractionOption.TradeProposal);

        Server.NetworkSentMessages.Clear();
        SubmitOption(client2, sessionId, responderPartyId, PlayerPartyInteractionOption.DeclineProposal);

        var ended = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionEnded>().Single();
        Assert.Equal(PlayerPartyInteractionOutcomeType.TradeDeclined, ended.OutcomeType);
        AssertInteractionStateCleared(client1);
        AssertInteractionStateCleared(client2);
    }

    [Fact]
    public void TradeOfferUpdate_RelaysThroughServerAndClearsAcceptState()
    {
        var (client1, client2, initiatorPartyId, responderPartyId) = CreateTwoPlayerParties();
        var sessionId = StartTrade(client1, client2, initiatorPartyId, responderPartyId);

        Server.NetworkSentMessages.Clear();
        client1.Call(() => client1.Resolve<INetwork>().SendAll(new NetworkPlayerPartyTradeOfferUpdated(
            sessionId,
            initiatorPartyId,
            new[]
            {
                new ItemRosterElementData(new ItemObjectData("item-1", null, itemModifierNull: true), 2)
            },
            new[] { new TroopRosterElementData("troop-1", 3, 0, 7) },
            offeredGold: 25,
            offeredFiefs: new[] { "fief-1" },
            offeredPrisoners: new[] { new TroopRosterElementData("prisoner-1", 1, 0, 0) })));

        var relayedOffer = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyTradeOfferUpdated>().Single();
        Assert.Equal(sessionId, relayedOffer.SessionId);
        Assert.Equal(initiatorPartyId, relayedOffer.PartyId);
        Assert.Single(relayedOffer.OfferedItems);
        Assert.Equal(2, relayedOffer.OfferedItems[0].Amount);
        Assert.Single(relayedOffer.OfferedTroops);
        Assert.Equal("troop-1", relayedOffer.OfferedTroops[0].CharacterId);
        Assert.Equal(3, relayedOffer.OfferedTroops[0].Number);
        Assert.Equal(25, relayedOffer.OfferedGold);
        Assert.Single(relayedOffer.OfferedFiefs);
        Assert.Equal("fief-1", relayedOffer.OfferedFiefs[0]);
        Assert.Single(relayedOffer.OfferedPrisoners);
        Assert.Equal("prisoner-1", relayedOffer.OfferedPrisoners[0].CharacterId);

        var states = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionState>().ToArray();
        Assert.Contains(states, s =>
            s.PartyId == initiatorPartyId &&
            !s.InitiatorAcceptedTrade &&
            !s.ResponderAcceptedTrade);
        Assert.Contains(states, s =>
            s.PartyId == responderPartyId &&
            !s.InitiatorAcceptedTrade &&
            !s.ResponderAcceptedTrade);
    }

    [Fact]
    public void TradeOfferUpdate_SpoofedResponderPartyId_UsesSenderParty()
    {
        var (client1, client2, initiatorPartyId, responderPartyId) = CreateTwoPlayerParties();
        var sessionId = StartTrade(client1, client2, initiatorPartyId, responderPartyId);

        Server.NetworkSentMessages.Clear();
        client1.Call(() => client1.Resolve<INetwork>().SendAll(new NetworkPlayerPartyTradeOfferUpdated(
            sessionId,
            responderPartyId,
            new[]
            {
                new ItemRosterElementData(new ItemObjectData("item-1", null, itemModifierNull: true), 2)
            },
            Array.Empty<TroopRosterElementData>())));

        var relayedOffer = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyTradeOfferUpdated>().Single();
        Assert.Equal(sessionId, relayedOffer.SessionId);
        Assert.Equal(initiatorPartyId, relayedOffer.PartyId);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void TradeOfferUpdate_AfterEitherPlayerAccepts_RelaysAndClearsAcceptState(
        bool initiatorAccepted,
        bool initiatorUpdates)
    {
        var (client1, client2, initiatorPartyId, responderPartyId) = CreateTwoPlayerParties();
        var sessionId = StartTrade(client1, client2, initiatorPartyId, responderPartyId);
        var acceptingClient = initiatorAccepted ? client1 : client2;
        var acceptedPartyId = initiatorAccepted ? initiatorPartyId : responderPartyId;
        var updatingClient = initiatorUpdates ? client1 : client2;
        var updaterPartyId = initiatorUpdates ? initiatorPartyId : responderPartyId;

        Server.NetworkSentMessages.Clear();
        acceptingClient.Call(() => acceptingClient.Resolve<INetwork>().SendAll(new NetworkPlayerPartyTradeAcceptChanged(sessionId, accepted: true)));
        Assert.Contains(Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionState>(), s =>
            s.PartyId == acceptedPartyId &&
            s.InitiatorAcceptedTrade == initiatorAccepted &&
            s.ResponderAcceptedTrade == !initiatorAccepted);

        Server.NetworkSentMessages.Clear();
        updatingClient.Call(() => updatingClient.Resolve<INetwork>().SendAll(new NetworkPlayerPartyTradeOfferUpdated(
            sessionId,
            updaterPartyId,
            new[]
            {
                new ItemRosterElementData(new ItemObjectData("item-1", null, itemModifierNull: true), 2)
            },
            Array.Empty<TroopRosterElementData>())));

        var relayedOffer = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyTradeOfferUpdated>().Single();
        Assert.Equal(sessionId, relayedOffer.SessionId);
        Assert.Equal(updaterPartyId, relayedOffer.PartyId);

        var states = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionState>().ToArray();
        Assert.Contains(states, s =>
            s.PartyId == initiatorPartyId &&
            !s.InitiatorAcceptedTrade &&
            !s.ResponderAcceptedTrade);
        Assert.Contains(states, s =>
            s.PartyId == responderPartyId &&
            !s.InitiatorAcceptedTrade &&
            !s.ResponderAcceptedTrade);
    }

    [Fact]
    public void TradeCancel_EndsInteractionAndAllowsNewInteraction()
    {
        var (client1, client2, initiatorPartyId, responderPartyId) = CreateTwoPlayerParties();
        var sessionId = StartTrade(client1, client2, initiatorPartyId, responderPartyId);

        Server.NetworkSentMessages.Clear();
        SubmitOption(client1, sessionId, initiatorPartyId, PlayerPartyInteractionOption.Leave);

        var ended = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionEnded>().Single();
        Assert.Equal(sessionId, ended.SessionId);
        Assert.Equal(PlayerPartyInteractionOutcomeType.TradeDeclined, ended.OutcomeType);
        AssertInteractionStateCleared(client1);
        AssertInteractionStateCleared(client2);

        Server.NetworkSentMessages.Clear();
        RequestInteraction(client1, initiatorPartyId, responderPartyId);

        var restarted = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionStarted>().Single();
        Assert.NotEqual(sessionId, restarted.SessionId);
        Assert.Equal(initiatorPartyId, restarted.InitiatorPartyId);
        Assert.Equal(responderPartyId, restarted.ResponderPartyId);
    }

    [Fact]
    public void HostileDemand_SelectedByInitiator_ShowsOfferPromptAndResponderChoices()
    {
        var (client1, client2, initiatorPartyId, responderPartyId) = CreateTwoPlayerParties();
        AssignDistinctClans(initiatorPartyId, responderPartyId);

        RequestInteraction(client1, initiatorPartyId, responderPartyId);
        var sessionId = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionStarted>().Single().SessionId;
        var initialState = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionState>().Single(s =>
            s.SessionId == sessionId &&
            s.PartyId == initiatorPartyId &&
            s.Phase == PlayerPartyInteractionPhase.InitialOptions);

        Assert.Contains(PlayerPartyInteractionOption.HostileDemand, initialState.Options);
        Assert.Contains(PlayerPartyInteractionOption.HostileDemand, initialState.EnabledOptions);

        Server.NetworkSentMessages.Clear();
        SubmitDialogOption(client1, initialState, PlayerPartyInteractionOption.HostileDemand);

        var confirmState = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionState>().Single(s =>
            s.PartyId == initiatorPartyId &&
            s.Phase == PlayerPartyInteractionPhase.HostileDemandConfirm);
        Assert.Equal(PlayerPartyInteractionProposal.HostileDemand, confirmState.Proposal);
        Assert.Equal(
            new[]
            {
                PlayerPartyInteractionOption.ConfirmHostileDemand,
                PlayerPartyInteractionOption.CancelHostileDemand
            },
            confirmState.Options);

        client1.Call(() =>
        {
            Assert.Equal(PlayerPartyInteractionPhase.HostileDemandConfirm, PlayerPartyInteractionDialogState.Phase);
            Assert.Equal("Eh? What do you want?", PlayerPartyInteractionDialogState.GetDialogText());
            Assert.True(PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.ConfirmHostileDemand));
            Assert.True(PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.CancelHostileDemand));
        });

        Server.NetworkSentMessages.Clear();
        SubmitOption(client1, sessionId, initiatorPartyId, PlayerPartyInteractionOption.ConfirmHostileDemand);

        var demandStates = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionState>().ToArray();
        Assert.Contains(demandStates, s =>
            s.PartyId == initiatorPartyId &&
            s.Phase == PlayerPartyInteractionPhase.WaitingForResponse &&
            s.Proposal == PlayerPartyInteractionProposal.HostileDemand);

        var responderState = demandStates.Single(s =>
            s.PartyId == responderPartyId &&
            s.Phase == PlayerPartyInteractionPhase.HostileDemandPending);
        Assert.Equal(PlayerPartyInteractionProposal.HostileDemand, responderState.Proposal);
        Assert.Equal(
            new[]
            {
                PlayerPartyInteractionOption.RefuseHostileDemand,
                PlayerPartyInteractionOption.YieldHostileDemand
            },
            responderState.Options);
        Assert.Equal(
            new[]
            {
                PlayerPartyInteractionOption.RefuseHostileDemand,
                PlayerPartyInteractionOption.YieldHostileDemand
            },
            responderState.EnabledOptions);
        Assert.DoesNotContain(PlayerPartyInteractionOption.Leave, responderState.Options);
        Assert.DoesNotContain(PlayerPartyInteractionOption.AcceptProposal, responderState.Options);
        Assert.DoesNotContain(PlayerPartyInteractionOption.DeclineProposal, responderState.Options);

        client2.Call(() =>
        {
            Assert.Equal(PlayerPartyInteractionPhase.HostileDemandPending, PlayerPartyInteractionDialogState.Phase);
            Assert.Equal("I offer you one chance to surrender or die", PlayerPartyInteractionDialogState.GetDialogText());
            Assert.True(PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.RefuseHostileDemand));
            Assert.True(PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.YieldHostileDemand));
            Assert.True(PlayerPartyInteractionDialogState.IsOptionEnabled(PlayerPartyInteractionOption.YieldHostileDemand));
            Assert.False(PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.Leave));
        });
    }

    [Fact]
    public void HostileDemand_ResponderCannotRefuseBeforeInitiatorConfirms()
    {
        var (client1, client2, initiatorPartyId, responderPartyId) = CreateTwoPlayerParties();
        AssignDistinctClans(initiatorPartyId, responderPartyId);

        RequestInteraction(client1, initiatorPartyId, responderPartyId);
        var sessionId = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionStarted>().Single().SessionId;

        SubmitOption(client1, sessionId, initiatorPartyId, PlayerPartyInteractionOption.HostileDemand);
        Server.NetworkSentMessages.Clear();
        SubmitOption(client2, sessionId, responderPartyId, PlayerPartyInteractionOption.RefuseHostileDemand);

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionEnded>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyHostileEncounterStarted>());
    }

    [Fact]
    public void HostileDemand_SameFaction_IsVisibleButDisabled()
    {
        var (client1, _, initiatorPartyId, responderPartyId) = CreateTwoPlayerParties();
        AssignSameClan(initiatorPartyId, responderPartyId);

        RequestInteraction(client1, initiatorPartyId, responderPartyId);
        var sessionId = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionStarted>().Single().SessionId;
        var initialState = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionState>().Single(s =>
            s.SessionId == sessionId &&
            s.PartyId == initiatorPartyId &&
            s.Phase == PlayerPartyInteractionPhase.InitialOptions);

        Assert.Contains(PlayerPartyInteractionOption.HostileDemand, initialState.Options);
        Assert.DoesNotContain(PlayerPartyInteractionOption.HostileDemand, initialState.EnabledOptions);

        Server.NetworkSentMessages.Clear();
        SubmitOption(client1, sessionId, initiatorPartyId, PlayerPartyInteractionOption.HostileDemand);

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionState>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionEnded>());
    }

    [Fact]
    public void HostileDemand_ResponderRefuses_DeclaresWarAndStartsEncounterMapEvent()
    {
        var (client1, client2, initiatorPartyId, responderPartyId) = CreateTwoPlayerParties();
        var (initiatorClanId, responderClanId) = AssignDistinctClans(initiatorPartyId, responderPartyId);

        RequestInteraction(client1, initiatorPartyId, responderPartyId);
        var sessionId = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionStarted>().Single().SessionId;
        SubmitOption(client1, sessionId, initiatorPartyId, PlayerPartyInteractionOption.HostileDemand);
        SubmitOption(client1, sessionId, initiatorPartyId, PlayerPartyInteractionOption.ConfirmHostileDemand);

        SetMainParty(client1, initiatorPartyId);
        SetMainParty(client2, responderPartyId);
        Server.NetworkSentMessages.Clear();
        SubmitOption(client2, sessionId, responderPartyId, PlayerPartyInteractionOption.RefuseHostileDemand, MapEventDisabledMethods);

        var ended = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionEnded>().Single();
        Assert.Equal(PlayerPartyInteractionOutcomeType.HostileDemandAccepted, ended.OutcomeType);

        var warDeclared = Server.NetworkSentMessages.GetMessages<NetworkDeclareWar>().Single();
        Assert.Equal(initiatorClanId, warDeclared.Faction1Id);
        Assert.Equal(responderClanId, warDeclared.Faction2Id);

        var hostileEncounterStarted = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyHostileEncounterStarted>().Single();
        Assert.Equal(sessionId, hostileEncounterStarted.SessionId);
        Assert.Equal(initiatorPartyId, hostileEncounterStarted.AttackerPartyId);
        Assert.Equal(responderPartyId, hostileEncounterStarted.DefenderPartyId);

        AssertWarDeclared(Server, initiatorClanId, responderClanId);
        AssertHostileEncounterMapEvent(Server, hostileEncounterStarted.MapEventId, initiatorPartyId, responderPartyId);
        foreach (var syncedClient in Clients)
        {
            Assert.Contains(syncedClient.InternalMessages.GetMessages<DeclareWarChanged>(), message =>
                message.Faction1Id == initiatorClanId &&
                message.Faction2Id == responderClanId);
            AssertWarDeclared(syncedClient, initiatorClanId, responderClanId);
            AssertHostileEncounterMapEvent(syncedClient, hostileEncounterStarted.MapEventId, initiatorPartyId, responderPartyId);
        }

        EnableHeadlessEncounterFinish(client1);
        EnableHeadlessEncounterFinish(client2);
        client1.Call(() =>
        {
            Assert.True(client1.ObjectManager.TryGetObject<MapEvent>(hostileEncounterStarted.MapEventId, out var mapEvent));
            client1.Resolve<IMessageBroker>().Publish(this, new MapEventFinalizeAttempted(mapEvent));
        }, HostileEncounterFinalizeDisabledMethods());

        AssertHostileEncounterTornDown(client1, initiatorPartyId);
        AssertHostileEncounterTornDown(client2, responderPartyId);
    }

    [Fact]
    public void HostileDemand_ResponderYields_DeclaresWarAndAutoSurrendersResponder()
    {
        var (client1, client2, _, responderHeroId, initiatorPartyId, responderPartyId) = CreateTwoPlayerPartiesWithHeroes();
        var initiatorMobilePartyId = GetMobilePartyId(Server, initiatorPartyId);
        var responderMobilePartyId = GetMobilePartyId(Server, responderPartyId);
        var (initiatorClanId, responderClanId) = AssignDistinctClans(initiatorPartyId, responderPartyId);
        PreparePlayerPartyForCapture(responderHeroId, responderPartyId);

        RequestInteraction(client1, initiatorPartyId, responderPartyId);
        var sessionId = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionStarted>().Single().SessionId;
        SubmitOption(client1, sessionId, initiatorPartyId, PlayerPartyInteractionOption.HostileDemand);
        SubmitOption(client1, sessionId, initiatorPartyId, PlayerPartyInteractionOption.ConfirmHostileDemand);

        // BR-061 baseline: the responder's heroes and regular troops become the initiator's prisoners on
        // the yield, so snapshot the rosters first — harness parties spawn with their own rosters, and raw
        // hero roster elements are never transferred (each hero is captured individually via
        // TakePrisonerAction: the responder hero directly, every other rider — the harness lord party's
        // bootstrap lord included — through the companion capture).
        var responderTroopsAtSurrender = GetPartyNonHeroManCount(Server, responderMobilePartyId);
        var responderRidingHeroes = GetPartyLiveHeroCount(Server, responderMobilePartyId);
        var initiatorPrisonersBefore = GetPartyPrisonerCount(Server, initiatorMobilePartyId);
        var initiatorPrisonersBeforeByClient = Clients.ToDictionary(c => c, c => GetPartyPrisonerCount(c, initiatorMobilePartyId));

        Server.NetworkSentMessages.Clear();
        SubmitOption(client2, sessionId, responderPartyId, PlayerPartyInteractionOption.YieldHostileDemand, HostileDemandSurrenderDisabledMethods());

        var ended = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionEnded>().Single();
        Assert.Equal(PlayerPartyInteractionOutcomeType.HostileDemandYielded, ended.OutcomeType);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyHostileEncounterStarted>());
        var closeEncounter = Server.NetworkSentMessages.GetMessages<NetworkClosePvpEncounter>().Single();
        Assert.Contains(initiatorPartyId, closeEncounter.PartyIds);
        Assert.Contains(responderPartyId, closeEncounter.PartyIds);

        var warDeclared = Server.NetworkSentMessages.GetMessages<NetworkDeclareWar>().Single();
        Assert.Equal(initiatorClanId, warDeclared.Faction1Id);
        Assert.Equal(responderClanId, warDeclared.Faction2Id);

        AssertWarDeclared(Server, initiatorClanId, responderClanId);
        AssertCaptivity(Server, responderHeroId, initiatorMobilePartyId);
        // BR-061: the yielded party's heroes AND regular troops are all recorded as the initiator's prisoners.
        AssertPartyPrisonerCount(Server, initiatorMobilePartyId, initiatorPrisonersBefore + responderTroopsAtSurrender + responderRidingHeroes);
        AssertPartyManCount(Server, responderMobilePartyId, 0);
        AssertHostileEncounterTornDown(Server, initiatorPartyId);
        AssertCapturedPlayerPartyParked(Server, responderPartyId);
        var leaderChanged = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkChangePartyLeader>());
        Assert.Equal(responderMobilePartyId, leaderChanged.MobilePartyId);
        Assert.Null(leaderChanged.LeaderHeroId);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPartyComponentLeaderChanged>());

        // The troop transfer replicates as coalesced roster deltas; drain them before reading client state.
        TestEnvironment.FlushCoalescer();

        foreach (var syncedClient in Clients)
        {
            Assert.Contains(syncedClient.InternalMessages.GetMessages<DeclareWarChanged>(), message =>
                message.Faction1Id == initiatorClanId &&
                message.Faction2Id == responderClanId);
            AssertWarDeclared(syncedClient, initiatorClanId, responderClanId);
            AssertCaptivity(syncedClient, responderHeroId, initiatorMobilePartyId);
            AssertPartyPrisonerCount(syncedClient, initiatorMobilePartyId,
                initiatorPrisonersBeforeByClient[syncedClient] + responderTroopsAtSurrender + responderRidingHeroes);
            AssertPartyManCount(syncedClient, responderMobilePartyId, 0);
            AssertHostileEncounterTornDown(syncedClient, initiatorPartyId);
            AssertCapturedPlayerPartyParked(syncedClient, responderPartyId);
        }
    }

    [Fact]
    public void HostilePlayerParties_RequestStartsDialogWithOfferServicesDisabledAndTradeAvailable()
    {
        var (client1, client2, initiatorPartyId, responderPartyId) = CreateTwoPlayerParties();
        MakePartiesHostile(initiatorPartyId, responderPartyId);

        RequestInteraction(client1, initiatorPartyId, responderPartyId);

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionDenied>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyHostileEncounterStarted>());

        var started = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionStarted>().Single();
        Assert.Equal(initiatorPartyId, started.InitiatorPartyId);
        Assert.Equal(responderPartyId, started.ResponderPartyId);

        var initialState = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionState>().Single(s =>
            s.SessionId == started.SessionId &&
            s.PartyId == initiatorPartyId &&
            s.Phase == PlayerPartyInteractionPhase.InitialOptions);
        Assert.True(initialState.IsHostile);
        Assert.Contains(PlayerPartyInteractionOption.TradeProposal, initialState.Options);
        Assert.Contains(PlayerPartyInteractionOption.TradeProposal, initialState.EnabledOptions);
        Assert.Contains(PlayerPartyInteractionOption.OfferServices, initialState.Options);
        Assert.DoesNotContain(PlayerPartyInteractionOption.OfferServices, initialState.EnabledOptions);
        Assert.Contains(PlayerPartyInteractionOption.HostileDemand, initialState.Options);
        Assert.Contains(PlayerPartyInteractionOption.HostileDemand, initialState.EnabledOptions);

        client1.Call(() =>
        {
            PlayerPartyInteractionDialogState.Apply(initialState);
            Assert.False(PlayerPartyInteractionDialogState.IsOptionEnabled(PlayerPartyInteractionOption.OfferServices, out var explanation));
            Assert.Equal("Not available while hostile", explanation.ToString());
            Assert.True(PlayerPartyInteractionDialogState.IsOptionEnabled(PlayerPartyInteractionOption.TradeProposal));
            Assert.True(PlayerPartyInteractionDialogState.IsOptionEnabled(PlayerPartyInteractionOption.HostileDemand));
        });

        Server.NetworkSentMessages.Clear();
        client1.NetworkSentMessages.Clear();
        OpenServiceOptions(client1, initialState);
        Assert.Empty(client1.NetworkSentMessages.GetMessages<NetworkSubmitPlayerPartyInteractionOption>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionState>());

        SubmitDialogOption(client1, initialState, PlayerPartyInteractionOption.TradeProposal);

        var proposalStates = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionState>().ToArray();
        Assert.Contains(proposalStates, s =>
            s.PartyId == initiatorPartyId &&
            s.Phase == PlayerPartyInteractionPhase.WaitingForResponse &&
            s.Proposal == PlayerPartyInteractionProposal.Trade);
        Assert.Contains(proposalStates, s =>
            s.PartyId == responderPartyId &&
            s.Phase == PlayerPartyInteractionPhase.ProposalPending &&
            s.Proposal == PlayerPartyInteractionProposal.Trade &&
            s.Options.Contains(PlayerPartyInteractionOption.AcceptProposal));
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyHostileEncounterStarted>());

        SubmitOption(client2, started.SessionId, responderPartyId, PlayerPartyInteractionOption.AcceptProposal);
        Assert.Contains(Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionState>(), s =>
            s.PartyId == initiatorPartyId &&
            s.Phase == PlayerPartyInteractionPhase.TradeActive);
    }

    [Fact]
    public void AiPartyConversation_UsesExistingAllowPath()
    {
        var (client1, _, initiatorPartyId, _) = CreateTwoPlayerParties();
        var aiPartyId = CreateMobilePartyBase();

        RequestInteraction(client1, initiatorPartyId, aiPartyId);

        var allowed = Server.NetworkSentMessages.GetMessages<NetworkAllowConversation>().Single();
        Assert.Equal(initiatorPartyId, allowed.AttackerId);
        Assert.Equal(aiPartyId, allowed.DefenderId);
        Assert.Equal("e2e-conversation-request", allowed.RequestId);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionStarted>());
        AssertInteractionStateCleared(client1);

        client1.Call(() => client1.Resolve<INetwork>().SendAll(
            new NetworkConversationEnded("e2e-conversation-request")));
    }

    [Fact]
    public void AiPartyConversation_AllowedThreadFinish_ReleasesServerEngagement()
    {
        var (client1, _, initiatorPartyId, _) = CreateTwoPlayerParties();
        var firstAiPartyId = CreateMobilePartyBase();
        var secondAiPartyId = CreateMobilePartyBase();

        SetMainParty(client1, initiatorPartyId);
        EnableHeadlessEncounterFinish(client1);
        SetMockPlayerEncounter(client1);
        client1.NetworkSentMessages.Clear();

        var disabledRouter = new[]
        {
            AccessTools.Method(
                typeof(TestNetworkRouter),
                nameof(TestNetworkRouter.SendAll),
                new[] { typeof(NetPeer), typeof(IMessage) }),
        };
        PublishConversationRequest(client1, initiatorPartyId, firstAiPartyId, disabledRouter);
        var request = Assert.Single(client1.NetworkSentMessages.GetMessages<NetworkRequestConversation>());
        Server.Call(() =>
        {
            var tracker = Server.Resolve<ConversationPartyTracker>();
            Assert.True(tracker.TryBeginEngagement(
                client1.NetPeer,
                initiatorPartyId,
                firstAiPartyId,
                wasAiDisabled: false,
                requestId: request.RequestId));
        });
        client1.NetworkSentMessages.Clear();

        client1.Call(() =>
        {
            using (new AllowedThread())
            {
                PlayerEncounter.Finish(false);
            }
        }, MapEventDisabledMethods);

        var ended = Assert.Single(client1.NetworkSentMessages.GetMessages<NetworkConversationEnded>());
        Assert.Equal(request.RequestId, ended.RequestId);
        AssertHasPlayerEncounter(client1, expected: false);
        Server.Call(() =>
            Assert.False(Server.Resolve<ConversationPartyTracker>().TryGetEngagement(client1.NetPeer, out _)));

        Server.NetworkSentMessages.Clear();
        RequestInteraction(client1, initiatorPartyId, secondAiPartyId);

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkAllowConversation>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkConversationDenied>());
    }

    [Fact]
    public void PendingReplacementRejected_WhenActiveEncounterFinishes_ReleasesActiveEngagement()
    {
        var (client, _, playerPartyId, _) = CreateTwoPlayerParties();
        var activeAiPartyId = CreateMobilePartyBase();
        var replacementAiPartyId = CreateMobilePartyBase();

        SetMainParty(client, playerPartyId);
        EnableHeadlessEncounterFinish(client);
        SetMockPlayerEncounter(client);
        var activeRequestId = CaptureConversationRestart(client);

        Server.Call(() =>
        {
            var tracker = Server.Resolve<ConversationPartyTracker>();
            Assert.True(tracker.TryBeginEngagement(
                client.NetPeer,
                playerPartyId,
                activeAiPartyId,
                wasAiDisabled: false,
                requestId: activeRequestId));
        });
        DeliverConversationApproval(
            client,
            playerPartyId,
            activeAiPartyId,
            activeRequestId,
            ConversationRestartSource.PlayerEncounter,
            forcePlayerOutFromSettlement: false);

        client.NetworkSentMessages.Clear();
        Server.NetworkSentMessages.Clear();
        // Let the server reject the replacement while its reply remains in flight.
        PublishConversationRequest(
            client,
            playerPartyId,
            replacementAiPartyId,
            new[] { GetDirectNetworkRoutingMethod() });

        var replacementRequest = Assert.Single(
            client.NetworkSentMessages.GetMessages<NetworkRequestConversation>());
        var denial = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkConversationDenied>());
        Assert.Equal(replacementRequest.RequestId, denial.RequestId);
        Assert.Equal(ConversationDeniedReason.PartyEngaged, denial.Reason);
        Server.Call(() =>
        {
            var tracker = Server.Resolve<ConversationPartyTracker>();
            Assert.True(tracker.TryGetEngagement(client.NetPeer, out var engagement));
            Assert.Equal(activeRequestId, engagement.RequestId);
        });

        client.NetworkSentMessages.Clear();
        client.Call(() =>
        {
            using (new AllowedThread())
            {
                PlayerEncounter.Finish(false);
            }
        }, MapEventDisabledMethods);

        var endedRequestIds = client.NetworkSentMessages
            .GetMessages<NetworkConversationEnded>()
            .Select(message => message.RequestId)
            .ToArray();
        Assert.Equal(2, endedRequestIds.Length);
        Assert.Contains(replacementRequest.RequestId, endedRequestIds);
        Assert.Contains(activeRequestId, endedRequestIds);
        AssertHasPlayerEncounter(client, expected: false);
        Server.Call(() =>
            Assert.False(Server.Resolve<ConversationPartyTracker>().TryGetEngagement(client.NetPeer, out _)));

        Server.NetworkSentMessages.Clear();
        RequestInteraction(client, playerPartyId, replacementAiPartyId);

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkAllowConversation>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkConversationDenied>());
    }

    [Fact]
    public async System.Threading.Tasks.Task ConversationDenial_QueuedBeforeRetry_DoesNotClearNewPendingRequest()
    {
        var (client, _, playerPartyId, _) = CreateTwoPlayerParties();
        var firstAiPartyId = CreateMobilePartyBase();
        var secondAiPartyId = CreateMobilePartyBase();
        var disabledRouter = new[] { GetNetworkRoutingMethod() };

        SetMockPlayerEncounter(client);
        PublishConversationRequest(client, playerPartyId, firstAiPartyId, disabledRouter);
        var firstRequest = Assert.Single(
            client.NetworkSentMessages.GetMessages<NetworkRequestConversation>());
        ResetConversationRequestCooldown(client);

        await System.Threading.Tasks.Task.Run(() =>
            client.SimulateMessage(
                Server.NetPeer,
                new NetworkConversationDenied(
                    ConversationDeniedReason.PartyEngaged,
                    firstRequest.RequestId)));
        Common.GameThread.Instance.MarkGameThread();

        Assert.Equal(firstRequest.RequestId, GetPendingConversationRequestId(client));

        client.NetworkSentMessages.Clear();
        PublishConversationRequest(client, playerPartyId, secondAiPartyId, disabledRouter);
        var secondRequest = Assert.Single(
            client.NetworkSentMessages.GetMessages<NetworkRequestConversation>());

        client.Call(() => Common.GameThread.Instance.Update(TimeSpan.Zero));

        Assert.Equal(secondRequest.RequestId, GetPendingConversationRequestId(client));

        client.NetworkSentMessages.Clear();
        client.Call(() =>
            client.Resolve<IMessageBroker>().Publish(this, new ConversationEnded()));

        var ended = Assert.Single(client.NetworkSentMessages.GetMessages<NetworkConversationEnded>());
        Assert.Equal(secondRequest.RequestId, ended.RequestId);
    }

    [Fact]
    public void ConversationApproval_ReplacesCapturedEncounter()
    {
        var (client, _, playerPartyId, _) = CreateTwoPlayerParties();
        var aiPartyId = CreateMobilePartyBase();

        SetMainParty(client, playerPartyId);
        EnableHeadlessEncounterFinish(client);
        var capturedEncounter = SetMockPlayerEncounter(client);
        var requestId = CaptureConversationRestart(client);

        DeliverConversationApproval(
            client,
            playerPartyId,
            aiPartyId,
            requestId,
            ConversationRestartSource.PlayerEncounter,
            forcePlayerOutFromSettlement: true);

        client.Call(() =>
        {
            Assert.NotSame(capturedEncounter, PlayerEncounter.Current);
            Assert.True(client.ObjectManager.TryGetObject<PartyBase>(aiPartyId, out var aiParty));
            Assert.Same(aiParty, PlayerEncounter.EncounteredParty);
        }, MapEventDisabledMethods);
    }

    [Fact]
    public async System.Threading.Tasks.Task ConversationApproval_PumpedNewerApproval_KeepsNewerRequestActive()
    {
        var client = Clients.First();
        client.Resolve<IControllerIdProvider>().SetControllerId("PlayerOne");
        var (_, playerMobilePartyId) = CreatePlayerHeroParty("PlayerOne");
        var aiMobilePartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var playerPartyId = GetPartyBaseId(Server, playerMobilePartyId);
        var aiPartyId = GetPartyBaseId(Server, aiMobilePartyId);

        Server.Resolve<IPlayerManager>().SetPeer("PlayerOne", client.NetPeer);
        SetMainParty(client, playerPartyId);
        EnableHeadlessEncounterFinish(client);
        SetMockPlayerEncounter(client);

        var firstRequestId = CaptureConversationRestart(client);
        var secondRequestId = CaptureConversationRestart(client);
        Server.Call(() =>
        {
            var tracker = Server.Resolve<ConversationPartyTracker>();
            Assert.True(tracker.TryBeginEngagement(
                client.NetPeer,
                playerPartyId,
                aiPartyId,
                wasAiDisabled: false,
                requestId: firstRequestId));
            Assert.True(tracker.TryBeginEngagement(
                client.NetPeer,
                playerPartyId,
                aiPartyId,
                wasAiDisabled: false,
                requestId: secondRequestId));
        });

        await System.Threading.Tasks.Task.Run(() =>
            client.SimulateMessage(
                Server.NetPeer,
                new NetworkAllowConversation(
                    aiPartyId,
                    playerPartyId,
                    forcePlayerOutFromSettlement: false,
                    ConversationRestartSource.EncounterManager,
                    secondRequestId)));
        Common.GameThread.Instance.MarkGameThread();
        Assert.True(Common.GameThread.Instance.QueueLength > 0);

        var getEncounterMenu = AccessTools.Method(
            typeof(DefaultEncounterGameMenuModel),
            nameof(DefaultEncounterGameMenuModel.GetEncounterMenu));
        Assert.NotNull(getEncounterMenu);
        var harmony = new Harmony($"{nameof(PlayerPartyInteractionFlowTests)}.approval-pump");
        harmony.Patch(
            getEncounterMenu,
            prefix: new HarmonyMethod(
                typeof(PlayerPartyInteractionFlowTests),
                nameof(ForceImmediateBattleEncounterMenu)));

        try
        {
            DeliverConversationApproval(
                client,
                playerPartyId,
                aiPartyId,
                firstRequestId,
                ConversationRestartSource.EncounterManager,
                forcePlayerOutFromSettlement: false,
                AccessTools.Method(
                    typeof(GameMenu),
                    nameof(GameMenu.ActivateGameMenu),
                    new[] { typeof(string) }));
        }
        finally
        {
            harmony.Unpatch(getEncounterMenu, HarmonyPatchType.Prefix, harmony.Id);
        }

        Assert.Equal(secondRequestId, GetActiveConversationRequestId(client));
        Server.Call(() =>
        {
            var tracker = Server.Resolve<ConversationPartyTracker>();
            Assert.True(tracker.TryGetEngagement(client.NetPeer, out var engagement));
            Assert.Equal(secondRequestId, engagement.RequestId);
        });

        client.NetworkSentMessages.Clear();
        client.Call(() =>
            client.Resolve<IMessageBroker>().Publish(this, new ConversationEnded()));

        var ended = Assert.Single(client.NetworkSentMessages.GetMessages<NetworkConversationEnded>());
        Assert.Equal(secondRequestId, ended.RequestId);
        Server.Call(() =>
            Assert.False(Server.Resolve<ConversationPartyTracker>().TryGetEngagement(client.NetPeer, out _)));
    }

    [Fact]
    public void ApprovedRestart_StartBattleInternal_UsesRegisteredServerMapEventForAttack()
    {
        var client = Clients.First();
        client.Resolve<IControllerIdProvider>().SetControllerId("PlayerOne");
        var (_, playerMobilePartyId) = CreatePlayerHeroParty("PlayerOne");
        var opponentMobilePartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var playerPartyId = GetPartyBaseId(Server, playerMobilePartyId);
        var opponentPartyId = GetPartyBaseId(Server, opponentMobilePartyId);
        Server.Resolve<IPlayerManager>().SetPeer("PlayerOne", client.NetPeer);
        SetMainParty(client, playerPartyId);

        string? mapEventId = null;
        try
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<PartyBase>(playerPartyId, out var playerParty));
                Assert.True(client.ObjectManager.TryGetObject<PartyBase>(opponentPartyId, out var opponentParty));

                var encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
                encounter._attackerParty = playerParty;
                encounter._defenderParty = opponentParty;
                Campaign.Current.PlayerEncounter = encounter;

                MapEvent? mapEvent;
                using (new AllowedThread())
                {
                    mapEvent = InvokePatchedStartBattleInternal(encounter);
                }

                Assert.NotNull(mapEvent);
                Assert.Same(mapEvent, encounter._mapEvent);
                Assert.Same(mapEvent, playerParty.MapEvent);
                Assert.Same(mapEvent, opponentParty.MapEvent);
                Assert.Contains(mapEvent, Campaign.Current.MapEventManager.MapEvents);
                Assert.True(client.ObjectManager.TryGetId(mapEvent, out mapEventId));
            }, MapEventDisabledMethods);

            Assert.NotNull(mapEventId);
            Server.Call(() => Assert.True(ServerBattleModeArbiter.TryClaimSimulation(mapEventId!)));
            client.NetworkSentMessages.Clear();
            Server.NetworkSentMessages.Clear();

            client.Call(() =>
            {
                var battleStartCoordinator = client.Resolve<BattleStartCoordinator>();
                // E2E clients share process statics, so select this simulated client's coordinator for the patch.
                AccessTools.Field(typeof(BattleStartCoordinator), "<Instance>k__BackingField")
                    .SetValue(null, battleStartCoordinator);
                Assert.Same(battleStartCoordinator, BattleStartCoordinator.Instance);
                InvokePatchedEncounterAttack();
                Assert.Same(battleStartCoordinator, BattleStartCoordinator.Instance);
            }, MapEventDisabledMethods);

            var request = Assert.Single(client.NetworkSentMessages.GetMessages<NetworkBattleStartRequest>());
            Assert.Equal((int)BattleStartMode.Mission, request.Mode);
            Assert.Equal(mapEventId, request.MapEventId);
            Assert.Equal(playerMobilePartyId, request.AttackerPartyId);
            Assert.False(Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBattleStartReply>()).Accepted);
        }
        finally
        {
            if (mapEventId != null)
                Server.Call(() => ServerBattleModeArbiter.Release(mapEventId));
        }
    }

    [Fact]
    public void DuplicateConversationApproval_DoesNotReplaceOpenEncounter()
    {
        var (client, _, playerPartyId, _) = CreateTwoPlayerParties();
        var aiPartyId = CreateMobilePartyBase();

        SetMainParty(client, playerPartyId);
        EnableHeadlessEncounterFinish(client);
        SetMockPlayerEncounter(client);
        var requestId = CaptureConversationRestart(client);
        DeliverConversationApproval(
            client,
            playerPartyId,
            aiPartyId,
            requestId,
            ConversationRestartSource.PlayerEncounter,
            forcePlayerOutFromSettlement: false);

        PlayerEncounter? openedEncounter = null;
        client.Call(() => openedEncounter = PlayerEncounter.Current);
        client.NetworkSentMessages.Clear();

        DeliverConversationApproval(
            client,
            playerPartyId,
            aiPartyId,
            requestId,
            ConversationRestartSource.PlayerEncounter,
            forcePlayerOutFromSettlement: false);

        client.Call(() => Assert.Same(openedEncounter, PlayerEncounter.Current));
        Assert.Empty(client.NetworkSentMessages.GetMessages<NetworkConversationEnded>());
    }

    [Fact]
    public void StaleConversationApproval_DoesNotReleaseNewerServerEngagement()
    {
        var (client, _, playerPartyId, _) = CreateTwoPlayerParties();
        var aiPartyId = CreateMobilePartyBase();
        var unrelatedPartyId = CreateMobilePartyBase();
        var capturedEncounter = SetMockPlayerEncounter(client);
        var staleRequestId = CaptureConversationRestart(client);
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<PartyBase>(unrelatedPartyId, out var unrelatedParty));
            capturedEncounter._encounteredParty = unrelatedParty;
        });
        var currentRequestId = CaptureConversationRestart(client);

        Server.Call(() =>
        {
            var tracker = Server.Resolve<ConversationPartyTracker>();
            Assert.True(tracker.TryBeginEngagement(
                client.NetPeer,
                playerPartyId,
                aiPartyId,
                wasAiDisabled: false,
                requestId: currentRequestId));
        });

        client.NetworkSentMessages.Clear();
        DeliverConversationApproval(
            client,
            playerPartyId,
            aiPartyId,
            staleRequestId,
            ConversationRestartSource.PlayerEncounter,
            forcePlayerOutFromSettlement: false);

        client.Call(() => Assert.Same(capturedEncounter, PlayerEncounter.Current));
        var ended = Assert.Single(client.NetworkSentMessages.GetMessages<NetworkConversationEnded>());
        Assert.Equal(staleRequestId, ended.RequestId);
        Server.Call(() =>
        {
            var tracker = Server.Resolve<ConversationPartyTracker>();
            Assert.True(tracker.TryGetEngagement(client.NetPeer, out var engagement));
            Assert.Equal(currentRequestId, engagement.RequestId);
        });

        client.Call(() => client.Resolve<INetwork>().SendAll(
            new NetworkConversationEnded(currentRequestId)));
    }

    [Fact]
    public void SallyOutConsequence_ApprovedWhileSiegeLeaderPending_OpensSiegeEncounter()
    {
        var (client, _, playerPartyId, _) = CreateTwoPlayerParties();
        var siege = CreateSyncedSiege();
        var pendingMapEventId = TestEnvironment.CreateRegisteredObject<MapEvent>(MapEventDisabledMethods);

        var capturedEncounter = PrepareClientSiegeEncounter(
            client,
            playerPartyId,
            siege.SettlementId);
        client.NetworkSentMessages.Clear();

        var captureDisabledMethods = MapEventDisabledMethods
            .Append(GetNetworkRoutingMethod())
            .ToList();
        client.Call(InvokeSallyOutConsequence, captureDisabledMethods);

        var request = Assert.Single(client.NetworkSentMessages.GetMessages<NetworkRequestConversation>());
        Assert.Equal(playerPartyId, request.AttackerId);
        Assert.Equal(siege.LeaderPartyId, request.DefenderId);
        Assert.Equal(ConversationRestartSource.EncounterManager, request.Source);
        client.Call(() => Assert.Same(capturedEncounter, PlayerEncounter.Current));

        MarkPartyPending(client, pendingMapEventId, siege.LeaderPartyId);

        DeliverConversationApproval(
            client,
            request.AttackerId,
            request.DefenderId,
            request.RequestId,
            request.Source,
            request.ForcePlayerOutFromSettlement,
            AccessTools.Method(
                typeof(DefaultEncounterGameMenuModel),
                nameof(DefaultEncounterGameMenuModel.GetEncounterMenu)));

        AssertApprovedSiegeEncounter(
            client,
            capturedEncounter,
            siege.SettlementId,
            siege.LeaderPartyId);
    }

    [Fact]
    public void BreakInContinuation_ServerEntryPrecedesApprovalAndOpensPendingSiegeEncounter()
    {
        var (client, _, playerPartyId, _) = CreateTwoPlayerParties();
        var playerMobilePartyId = GetMobilePartyId(Server, playerPartyId);
        var siege = CreateSyncedSiege();
        var pendingMapEventId = TestEnvironment.CreateRegisteredObject<MapEvent>(MapEventDisabledMethods);
        TestEnvironment.ConnectRegisteredPlayer(client, "PlayerOne");
        PrepareBreakInDefenderEligibility(
            playerPartyId,
            siege.SettlementId,
            siege.LeaderPartyId);

        var capturedEncounter = PrepareClientSiegeEncounter(
            client,
            playerPartyId,
            siege.SettlementId,
            enterSettlement: false);
        client.NetworkSentMessages.Clear();
        Server.NetworkSentMessages.Clear();

        var captureRequestDisabledMethods = MapEventDisabledMethods
            .Append(GetNetworkRoutingMethod())
            .ToList();
        client.Call(InvokeBreakInContinuation, captureRequestDisabledMethods);

        var breakInRequest = Assert.Single(
            client.NetworkSentMessages.GetMessages<NetworkRequestBreakInContinuation>());
        Assert.Equal(playerMobilePartyId, breakInRequest.PartyId);
        Assert.Equal(siege.SettlementId, breakInRequest.SettlementId);
        Assert.Empty(client.NetworkSentMessages.GetMessages<NetworkRequestConversation>());
        AssertPartyOutsideSettlement(Server, playerMobilePartyId);
        foreach (var syncedClient in Clients)
            AssertPartyOutsideSettlement(syncedClient, playerMobilePartyId);
        LocationEncounter? stagedLocationEncounter = null;
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(
                siege.SettlementId,
                out var settlement));
            stagedLocationEncounter = PlayerEncounter.LocationEncounter;
            Assert.NotNull(stagedLocationEncounter);
            Assert.Same(settlement, stagedLocationEncounter!.Settlement);
        });

        client.SimulateMessage(
            Server.NetPeer,
            new NetworkBreakInContinuationApproved(
                "stale-break-in-request",
                siege.SettlementId,
                approved: true));
        Assert.Empty(client.NetworkSentMessages.GetMessages<NetworkRequestConversation>());
        AssertPartyOutsideSettlement(client, playerMobilePartyId);
        client.Call(() => Assert.Same(stagedLocationEncounter, PlayerEncounter.LocationEncounter));

        client.SimulateMessage(
            Server.NetPeer,
            new NetworkBreakInContinuationApproved(
                breakInRequest.RequestId,
                siege.SettlementId,
                approved: false));
        client.Call(() => Assert.Null(PlayerEncounter.LocationEncounter));

        client.NetworkSentMessages.Clear();
        client.Call(InvokeBreakInContinuation, captureRequestDisabledMethods);

        var firstRequestId = breakInRequest.RequestId;
        breakInRequest = Assert.Single(
            client.NetworkSentMessages.GetMessages<NetworkRequestBreakInContinuation>());
        Assert.NotEqual(firstRequestId, breakInRequest.RequestId);
        client.Call(() =>
        {
            stagedLocationEncounter = PlayerEncounter.LocationEncounter;
            Assert.NotNull(stagedLocationEncounter);
        });

        Server.Call(
            () =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                    playerMobilePartyId,
                    out var playerParty));
                Assert.True(Server.ObjectManager.TryGetObject<Settlement>(
                    siege.SettlementId,
                    out var settlement));
                Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(
                    siege.LeaderPartyId,
                    out var siegeLeaderParty));

                VillageHostileFactionStanceHelper.ApplyWarStance(
                    siegeLeaderParty.MapFaction,
                    playerParty.MapFaction);
                Assert.True(playerParty.IsActive);
                Assert.Null(playerParty.CurrentSettlement);
                Assert.Null(playerParty.BesiegerCamp);
                Assert.Null(playerParty.Party.MapEventSide);
                Assert.NotNull(settlement.SiegeEvent);
                Assert.True(settlement.SiegeEvent.CanPartyJoinSide(
                    playerParty.Party,
                    BattleSideEnum.Defender));

                Server.SimulateMessage(client.NetPeer, breakInRequest);
            },
            MapEventDisabledMethods.Append(GetDirectNetworkRoutingMethod()));

        var settlementEntry = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkPartyEnterSettlement>());
        Assert.Equal(
            ObjectManager.Compact(playerMobilePartyId, typeof(MobileParty)),
            settlementEntry.PartyId);
        Assert.Equal(
            ObjectManager.Compact(siege.SettlementId, typeof(Settlement)),
            settlementEntry.SettlementId);
        var breakInApproval = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkBreakInContinuationApproved>());
        Assert.Equal(breakInRequest.RequestId, breakInApproval.RequestId);
        Assert.True(breakInApproval.Approved);
        Assert.Empty(client.NetworkSentMessages.GetMessages<NetworkRequestConversation>());
        AssertPartyEnteredSettlement(Server, playerMobilePartyId, siege.SettlementId);
        foreach (var syncedClient in Clients)
            AssertPartyEnteredSettlement(syncedClient, playerMobilePartyId, siege.SettlementId);
        client.Call(() =>
        {
            Assert.Same(capturedEncounter, PlayerEncounter.Current);
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(
                siege.SettlementId,
                out var settlement));
            Assert.Same(settlement, PlayerEncounter.EncounterSettlement);
            Assert.NotNull(Campaign.Current.GetCampaignBehavior<EncounterGameMenuBehavior>());
        });

        client.NetworkSentMessages.Clear();

        var captureContinuationDisabledMethods = MapEventDisabledMethods
            .Append(GetNetworkRoutingMethod())
            .Append(AccessTools.Method(typeof(PlayerSiege), nameof(PlayerSiege.StartSiegePreparation)))
            .ToList();
        client.Call(
            () => client.SimulateMessage(Server.NetPeer, breakInApproval),
            captureContinuationDisabledMethods);

        var request = Assert.Single(client.NetworkSentMessages.GetMessages<NetworkRequestConversation>());
        Assert.Equal(siege.LeaderPartyId, request.AttackerId);
        Assert.Equal(playerPartyId, request.DefenderId);
        Assert.Equal(ConversationRestartSource.PlayerEncounter, request.Source);
        client.Call(() => Assert.Same(capturedEncounter, PlayerEncounter.Current));

        MarkPartyPending(client, pendingMapEventId, siege.LeaderPartyId);

        DeliverConversationApproval(
            client,
            request.AttackerId,
            request.DefenderId,
            request.RequestId,
            request.Source,
            request.ForcePlayerOutFromSettlement);

        AssertApprovedSiegeEncounter(
            client,
            capturedEncounter,
            siege.SettlementId,
            siege.LeaderPartyId);
    }

    [Fact]
    public void BreakInContinuation_ApprovedAfterEncounterChanges_PreservesEnteredLocation()
    {
        var (client, _, playerPartyId, _) = CreateTwoPlayerParties();
        var playerMobilePartyId = GetMobilePartyId(Server, playerPartyId);
        var siege = CreateSyncedSiege();
        TestEnvironment.ConnectRegisteredPlayer(client, "PlayerOne");
        PrepareBreakInDefenderEligibility(
            playerPartyId,
            siege.SettlementId,
            siege.LeaderPartyId);
        PrepareClientSiegeEncounter(
            client,
            playerPartyId,
            siege.SettlementId,
            enterSettlement: false);
        client.NetworkSentMessages.Clear();

        var captureRequestDisabledMethods = MapEventDisabledMethods
            .Append(GetNetworkRoutingMethod())
            .ToList();
        client.Call(InvokeBreakInContinuation, captureRequestDisabledMethods);

        var request = Assert.Single(
            client.NetworkSentMessages.GetMessages<NetworkRequestBreakInContinuation>());
        LocationEncounter? stagedLocationEncounter = null;
        client.Call(() =>
        {
            stagedLocationEncounter = PlayerEncounter.LocationEncounter;
            Assert.NotNull(stagedLocationEncounter);
        });

        client.SimulateMessage(
            Server.NetPeer,
            new NetworkPartyEnterSettlement(
                ObjectManager.Compact(siege.SettlementId, typeof(Settlement)),
                ObjectManager.Compact(playerMobilePartyId, typeof(MobileParty))));
        AssertPartyEnteredSettlement(client, playerMobilePartyId, siege.SettlementId);

        PlayerEncounter? changedEncounter = null;
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(
                siege.SettlementId,
                out var settlement));
            changedEncounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
            changedEncounter.EncounterSettlementAux = settlement;
            Campaign.Current.PlayerEncounter = changedEncounter;
        });

        client.SimulateMessage(
            Server.NetPeer,
            new NetworkBreakInContinuationApproved(
                request.RequestId,
                request.SettlementId,
                approved: true));

        Assert.Empty(client.NetworkSentMessages.GetMessages<NetworkRequestConversation>());
        AssertPartyEnteredSettlement(client, playerMobilePartyId, siege.SettlementId);
        client.Call(() =>
        {
            Assert.Same(changedEncounter, PlayerEncounter.Current);
            Assert.Same(stagedLocationEncounter, PlayerEncounter.LocationEncounter);
        });
    }

    [Fact]
    public void ServerAiPartyEncounter_StartsConversationForDefendingPlayer()
    {
        var client = Clients.First();
        client.Resolve<IControllerIdProvider>().SetControllerId("PlayerOne");
        var (_, playerMobilePartyId) = CreatePlayerHeroParty("PlayerOne");
        var playerPartyId = GetPartyBaseId(Server, playerMobilePartyId);
        var aiPartyId = CreateMobilePartyBase();

        Server.Resolve<IPlayerManager>().SetPeer("PlayerOne", client.NetPeer);
        Server.NetworkSentMessages.Clear();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(aiPartyId, out var aiParty));
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(playerPartyId, out var playerParty));

            EncounterManager.StartPartyEncounter(aiParty, playerParty);

            Assert.Null(aiParty.MapEvent);
            Assert.Null(playerParty.MapEvent);
        });

        var allowed = Server.NetworkSentMessages.GetMessages<NetworkAllowConversation>().Single();
        Assert.Equal(aiPartyId, allowed.AttackerId);
        Assert.Equal(playerPartyId, allowed.DefenderId);
        Assert.Null(allowed.RequestId);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkConversationDenied>());
    }

    [Fact]
    public void NpcPeaceBarter_ActiveHostileEncounter_AppliesPaymentPeaceAndReleasesConversation()
    {
        const int initialPlayerGold = 1_000_000;
        const int initialTargetGold = 40;
        const int offeredGold = 500_000;

        var client = Clients.First();
        client.Resolve<IControllerIdProvider>().SetControllerId("PlayerOne");
        var (playerHeroId, playerMobilePartyId) = CreatePlayerPartyWithRegisteredLeader("PlayerOne");
        var playerPartyId = GetPartyBaseId(Server, playerMobilePartyId);
        var (targetHeroId, targetMobilePartyId, targetPartyId) = CreateAiPartyWithRegisteredLeader();
        var (playerClanId, targetClanId) = MakePartiesHostile(playerPartyId, targetPartyId);

        Server.Resolve<IPlayerManager>().SetPeer("PlayerOne", client.NetPeer);
        Server.Call(() =>
        {
            new GoldBarterBehavior().RegisterEvents();

            Assert.True(Server.ObjectManager.TryGetObject<Hero>(playerHeroId, out var playerHero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(targetHeroId, out var targetHero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(playerMobilePartyId, out var playerParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(targetMobilePartyId, out var targetParty));

            playerHero.Gold = initialPlayerGold;
            targetHero.Gold = initialTargetGold;
            VillageHostileFactionStanceHelper.ApplyWarStance(playerHero.MapFaction, targetHero.MapFaction);
            Assert.True(FactionManager.IsAtWarAgainstFaction(playerHero.MapFaction, targetHero.MapFaction));
            targetParty.SetMoveEngageParty(playerParty, MobileParty.NavigationType.Default);
            Assert.True(ConversationPartyHold.TryEngage(
                Server.Resolve<ConversationPartyTracker>(),
                client.NetPeer,
                playerPartyId,
                targetParty,
                targetPartyId,
                engagerIsDefender: true));
        });
        TestEnvironment.FlushCoalescer();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(playerHeroId, out var playerHero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(targetHeroId, out var targetHero));
            Assert.True(FactionManager.IsAtWarAgainstFaction(playerHero.MapFaction, targetHero.MapFaction));
        });

        Server.NetworkSentMessages.Clear();
        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkRequestPeaceBarter(
            targetHeroId,
            PeaceConversationContext.MapParty,
            targetPartyId,
            new[]
            {
                new PeaceBarterTerm(
                    PeaceBarterTermType.Gold,
                    playerHeroId,
                    objectId: null,
                    itemModifierId: null,
                    itemModifierNull: true,
                    amount: offeredGold),
            },
            requestId: "map-peace-success")));

        var result = Server.NetworkSentMessages.GetMessages<NetworkPeaceBarterResult>().Single();
        Assert.True(result.Accepted, result.Reason);
        Assert.Equal(targetPartyId, result.ContextId);
        Assert.Equal("map-peace-success", result.RequestId);
        Assert.Equal(initialPlayerGold - offeredGold, result.PlayerGold);

        foreach (var environmentClient in Clients)
        {
            environmentClient.Call(() =>
            {
                Assert.True(environmentClient.ObjectManager.TryGetObject<Hero>(playerHeroId, out var playerHero));
                Assert.Equal(initialPlayerGold - offeredGold, playerHero.Gold);
            });
        }

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(playerHeroId, out var playerHero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(targetHeroId, out var targetHero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(targetMobilePartyId, out var targetParty));
            Assert.Equal(initialPlayerGold - offeredGold, playerHero.Gold);
            Assert.Equal(initialTargetGold + offeredGold, targetHero.Gold);

            var tracker = Server.Resolve<ConversationPartyTracker>();
            Assert.False(tracker.TryGetEngagement(client.NetPeer, out _));
            Assert.False(targetParty.Ai.IsDisabled);
        });

        var peaceMade = Server.NetworkSentMessages.GetMessages<NetworkMakePeace>().Single();
        Assert.Equal(playerClanId, peaceMade.Faction1Id);
        Assert.Equal(targetClanId, peaceMade.Faction2Id);
        AssertPeaceMade(Server, playerClanId, targetClanId);
        foreach (var environmentClient in Clients)
            AssertPeaceMade(environmentClient, playerClanId, targetClanId);
    }

    [Fact]
    public void NpcPeaceBarter_DifferentEngagedParty_RejectsWithoutEffects()
    {
        const int initialPlayerGold = 1_000_000;
        const int initialRequestedTargetGold = 40;
        const int offeredGold = 500_000;

        var client = Clients.First();
        client.Resolve<IControllerIdProvider>().SetControllerId("PlayerOne");
        var (playerHeroId, playerMobilePartyId) = CreatePlayerPartyWithRegisteredLeader("PlayerOne");
        var playerPartyId = GetPartyBaseId(Server, playerMobilePartyId);
        var (_, activeTargetMobilePartyId, activeTargetPartyId) = CreateAiPartyWithRegisteredLeader();
        var (requestedTargetHeroId, requestedTargetMobilePartyId, requestedTargetPartyId) = CreateAiPartyWithRegisteredLeader();
        var (playerClanId, _) = MakePartiesHostile(playerPartyId, activeTargetPartyId);
        var requestedTargetClanId = TestEnvironment.CreateRegisteredObject<Clan>();

        Server.Resolve<IPlayerManager>().SetPeer("PlayerOne", client.NetPeer);
        Server.Call(() =>
        {
            new GoldBarterBehavior().RegisterEvents();

            Assert.True(Server.ObjectManager.TryGetObject<Clan>(playerClanId, out var playerClan));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(requestedTargetClanId, out var requestedTargetClan));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(playerHeroId, out var playerHero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(requestedTargetHeroId, out var requestedTargetHero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(playerMobilePartyId, out var playerParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(activeTargetMobilePartyId, out var activeTargetParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(requestedTargetMobilePartyId, out var requestedTargetParty));

            requestedTargetHero.Clan = requestedTargetClan;
            requestedTargetClan.SetLeader(requestedTargetHero);
            requestedTargetParty.ActualClan = requestedTargetClan;
            VillageHostileFactionStanceHelper.ApplyWarStance(playerClan, requestedTargetClan);
            Assert.True(FactionManager.IsAtWarAgainstFaction(playerHero.MapFaction, requestedTargetHero.MapFaction));

            playerHero.Gold = initialPlayerGold;
            requestedTargetHero.Gold = initialRequestedTargetGold;
            activeTargetParty.SetMoveEngageParty(playerParty, MobileParty.NavigationType.Default);
            EncounterManager.StartPartyEncounter(activeTargetParty.Party, playerParty.Party);
        });
        TestEnvironment.FlushCoalescer();

        var allowed = Server.NetworkSentMessages.GetMessages<NetworkAllowConversation>().Single();
        Assert.Equal(activeTargetPartyId, allowed.AttackerId);
        Assert.Equal(playerPartyId, allowed.DefenderId);

        Server.NetworkSentMessages.Clear();
        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkRequestPeaceBarter(
            requestedTargetHeroId,
            PeaceConversationContext.MapParty,
            requestedTargetPartyId,
            new[]
            {
                new PeaceBarterTerm(
                    PeaceBarterTermType.Gold,
                    playerHeroId,
                    objectId: null,
                    itemModifierId: null,
                    itemModifierNull: true,
                    amount: offeredGold),
            },
            requestId: "map-peace-mismatch")));

        var result = Server.NetworkSentMessages.GetMessages<NetworkPeaceBarterResult>().Single();
        Assert.False(result.Accepted);
        Assert.Equal(requestedTargetPartyId, result.ContextId);
        Assert.Equal("map-peace-mismatch", result.RequestId);
        Assert.Equal(initialPlayerGold, result.PlayerGold);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkMakePeace>());

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(playerHeroId, out var playerHero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(requestedTargetHeroId, out var requestedTargetHero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(activeTargetMobilePartyId, out var activeTargetParty));
            Assert.Equal(initialPlayerGold, playerHero.Gold);
            Assert.Equal(initialRequestedTargetGold, requestedTargetHero.Gold);

            var tracker = Server.Resolve<ConversationPartyTracker>();
            Assert.True(tracker.TryGetEngagement(client.NetPeer, out var engagement));
            Assert.Equal(activeTargetPartyId, engagement.PartyId);
            Assert.True(activeTargetParty.Ai.IsDisabled);
        });
        AssertWarDeclared(Server, playerClanId, requestedTargetClanId);

        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkConversationEnded()));
    }

    [Fact]
    public void NpcPeaceBarter_ActiveLocationConversation_AppliesPaymentAndPeace()
    {
        const string locationId = "e2e_peace_location";
        const string requestId = "location-peace-success";
        const int initialPlayerGold = 1_000_000;
        const int initialTargetGold = 40;
        const int offeredGold = 500_000;

        var client = Clients.First();
        client.Resolve<IControllerIdProvider>().SetControllerId("PlayerOne");
        var (playerHeroId, playerMobilePartyId) = CreatePlayerPartyWithRegisteredLeader("PlayerOne");
        var playerPartyId = GetPartyBaseId(Server, playerMobilePartyId);
        var (targetHeroId, _, targetPartyId) = CreateAiPartyWithRegisteredLeader();
        var (playerClanId, targetClanId) = MakePartiesHostile(playerPartyId, targetPartyId);

        Server.Resolve<IPlayerManager>().SetPeer("PlayerOne", client.NetPeer);
        Server.Call(() =>
        {
            new GoldBarterBehavior().RegisterEvents();

            Assert.True(Server.ObjectManager.TryGetObject<Hero>(playerHeroId, out var playerHero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(targetHeroId, out var targetHero));
            Assert.True(Server.ObjectManager.TryGetId(playerHero.CharacterObject, out var playerCharacterId));
            Assert.True(Server.ObjectManager.TryGetId(targetHero.CharacterObject, out var targetCharacterId));

            playerHero.Gold = initialPlayerGold;
            targetHero.Gold = initialTargetGold;
            VillageHostileFactionStanceHelper.ApplyWarStance(playerHero.MapFaction, targetHero.MapFaction);
            Assert.True(FactionManager.IsAtWarAgainstFaction(playerHero.MapFaction, targetHero.MapFaction));
            Assert.True(Server.Resolve<LocationConversationTracker>().TryBeginEngagement(
                client.NetPeer,
                LocationConversationTracker.ComposeKey(locationId, playerCharacterId),
                LocationConversationTracker.ComposeKey(locationId, targetCharacterId)));
        });
        TestEnvironment.FlushCoalescer();

        Server.NetworkSentMessages.Clear();
        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkRequestPeaceBarter(
            targetHeroId,
            PeaceConversationContext.Location,
            locationId,
            new[]
            {
                new PeaceBarterTerm(
                    PeaceBarterTermType.Gold,
                    playerHeroId,
                    objectId: null,
                    itemModifierId: null,
                    itemModifierNull: true,
                    amount: offeredGold),
            },
            requestId)));

        var result = Server.NetworkSentMessages.GetMessages<NetworkPeaceBarterResult>().Single();
        Assert.True(result.Accepted, result.Reason);
        Assert.Equal(locationId, result.ContextId);
        Assert.Equal(requestId, result.RequestId);
        Assert.Equal(initialPlayerGold - offeredGold, result.PlayerGold);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(playerHeroId, out var playerHero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(targetHeroId, out var targetHero));
            Assert.Equal(initialPlayerGold - offeredGold, playerHero.Gold);
            Assert.Equal(initialTargetGold + offeredGold, targetHero.Gold);
            Assert.True(Server.Resolve<LocationConversationTracker>().TryGetEngagement(client.NetPeer, out var npcKey));
            Assert.StartsWith(locationId + "|", npcKey);
        });

        var peaceMade = Server.NetworkSentMessages.GetMessages<NetworkMakePeace>().Single();
        Assert.Equal(playerClanId, peaceMade.Faction1Id);
        Assert.Equal(targetClanId, peaceMade.Faction2Id);
        AssertPeaceMade(Server, playerClanId, targetClanId);
        foreach (var environmentClient in Clients)
        {
            environmentClient.Call(() =>
            {
                Assert.True(environmentClient.ObjectManager.TryGetObject<Hero>(playerHeroId, out var playerHero));
                Assert.Equal(initialPlayerGold - offeredGold, playerHero.Gold);
            });
            AssertPeaceMade(environmentClient, playerClanId, targetClanId);
        }

        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkLocationConversationEnded()));
    }

    [Fact]
    public void NpcPeaceBarter_DifferentLocation_RejectsWithoutEffects()
    {
        const string activeLocationId = "e2e_active_peace_location";
        const string requestedLocationId = "e2e_wrong_peace_location";
        const string requestId = "location-peace-mismatch";
        const int initialPlayerGold = 1_000_000;
        const int initialTargetGold = 40;
        const int offeredGold = 500_000;

        var client = Clients.First();
        client.Resolve<IControllerIdProvider>().SetControllerId("PlayerOne");
        var (playerHeroId, playerMobilePartyId) = CreatePlayerPartyWithRegisteredLeader("PlayerOne");
        var playerPartyId = GetPartyBaseId(Server, playerMobilePartyId);
        var (targetHeroId, _, targetPartyId) = CreateAiPartyWithRegisteredLeader();
        var (playerClanId, targetClanId) = MakePartiesHostile(playerPartyId, targetPartyId);

        Server.Resolve<IPlayerManager>().SetPeer("PlayerOne", client.NetPeer);
        Server.Call(() =>
        {
            new GoldBarterBehavior().RegisterEvents();

            Assert.True(Server.ObjectManager.TryGetObject<Hero>(playerHeroId, out var playerHero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(targetHeroId, out var targetHero));
            Assert.True(Server.ObjectManager.TryGetId(playerHero.CharacterObject, out var playerCharacterId));
            Assert.True(Server.ObjectManager.TryGetId(targetHero.CharacterObject, out var targetCharacterId));

            playerHero.Gold = initialPlayerGold;
            targetHero.Gold = initialTargetGold;
            VillageHostileFactionStanceHelper.ApplyWarStance(playerHero.MapFaction, targetHero.MapFaction);
            Assert.True(Server.Resolve<LocationConversationTracker>().TryBeginEngagement(
                client.NetPeer,
                LocationConversationTracker.ComposeKey(activeLocationId, playerCharacterId),
                LocationConversationTracker.ComposeKey(activeLocationId, targetCharacterId)));
        });
        TestEnvironment.FlushCoalescer();

        Server.NetworkSentMessages.Clear();
        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkRequestPeaceBarter(
            targetHeroId,
            PeaceConversationContext.Location,
            requestedLocationId,
            new[]
            {
                new PeaceBarterTerm(
                    PeaceBarterTermType.Gold,
                    playerHeroId,
                    objectId: null,
                    itemModifierId: null,
                    itemModifierNull: true,
                    amount: offeredGold),
            },
            requestId)));

        var result = Server.NetworkSentMessages.GetMessages<NetworkPeaceBarterResult>().Single();
        Assert.False(result.Accepted);
        Assert.Equal(requestedLocationId, result.ContextId);
        Assert.Equal(requestId, result.RequestId);
        Assert.Equal(initialPlayerGold, result.PlayerGold);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkMakePeace>());

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(playerHeroId, out var playerHero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(targetHeroId, out var targetHero));
            Assert.Equal(initialPlayerGold, playerHero.Gold);
            Assert.Equal(initialTargetGold, targetHero.Gold);
            Assert.True(Server.Resolve<LocationConversationTracker>().TryGetEngagement(client.NetPeer, out var npcKey));
            Assert.StartsWith(activeLocationId + "|", npcKey);
        });
        AssertWarListsContainEachOther(Server, playerClanId, targetClanId);

        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkLocationConversationEnded()));
    }

    [Fact]
    public void BanditSafePassage_ActiveEncounter_AppliesPaymentAndSurvivesConversationRelease()
    {
        const int initialPlayerGold = 1000;
        const int initialBanditGold = 40;
        const int offeredGold = 250;

        var client = Clients.First();
        client.Resolve<IControllerIdProvider>().SetControllerId("PlayerOne");
        var (playerHeroId, playerMobilePartyId) = CreatePlayerPartyWithRegisteredLeader("PlayerOne");
        var playerPartyId = GetPartyBaseId(Server, playerMobilePartyId);
        var banditMobilePartyId = CreateBanditParty();
        var joiningBanditMobilePartyId = CreateBanditParty("E2EBanditSafePassageJoiner");

        Server.Resolve<IPlayerManager>().SetPeer("PlayerOne", client.NetPeer);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(playerHeroId, out var playerHero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(playerMobilePartyId, out var playerParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(banditMobilePartyId, out var banditParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(joiningBanditMobilePartyId, out var joiningBanditParty));

            Server.Resolve<ISessionInteractionsPlayerDataInterface>().AddPlayerKeys(playerHeroId);
            playerHero.Gold = initialPlayerGold;
            banditParty.PartyTradeGold = initialBanditGold;
            VillageHostileFactionStanceHelper.ApplyWarStance(
                banditParty.MapFaction,
                playerParty.MapFaction);
            VillageHostileFactionStanceHelper.ApplyWarStance(
                joiningBanditParty.MapFaction,
                playerParty.MapFaction);
            Assert.True(joiningBanditParty.MapFaction.IsAtWarWith(playerParty.MapFaction));
            Assert.False(joiningBanditParty.MapFaction.IsAtWarWith(banditParty.MapFaction));
            joiningBanditParty.AttachedTo = banditParty;
            Assert.Same(banditParty, joiningBanditParty.AttachedTo);
            Assert.Contains(joiningBanditParty, banditParty.AttachedParties);
            banditParty.SetMoveEngageParty(playerParty, MobileParty.NavigationType.Default);
            joiningBanditParty.SetMoveEngageParty(playerParty, MobileParty.NavigationType.Default);

            Assert.False(banditParty.Ai.IsDisabled);
            EncounterManager.StartPartyEncounter(banditParty.Party, playerParty.Party);
        });
        TestEnvironment.FlushCoalescer();

        var allowed = Server.NetworkSentMessages.GetMessages<NetworkAllowConversation>().Single();
        Assert.Equal(GetPartyBaseId(Server, banditMobilePartyId), allowed.AttackerId);
        Assert.Equal(playerPartyId, allowed.DefenderId);

        Server.NetworkSentMessages.Clear();
        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkRequestBanditBarter(
            banditMobilePartyId,
            offeredGold,
            Array.Empty<ItemRosterElementData>(),
            Array.Empty<TroopRosterElementData>(),
            "bandit-payment")));

        var result = Server.NetworkSentMessages.GetMessages<NetworkBanditBarterResult>().Single();
        Assert.True(result.Accepted, result.Reason);
        Assert.Equal(banditMobilePartyId, result.BanditPartyId);
        Assert.Equal(initialPlayerGold - offeredGold, result.PlayerGold);

        foreach (var environmentClient in Clients)
        {
            environmentClient.Call(() =>
            {
                Assert.True(environmentClient.ObjectManager.TryGetObject<Hero>(playerHeroId, out var playerHero));
                Assert.Equal(initialPlayerGold - offeredGold, playerHero.Gold);
            });
        }

        TestEnvironment.FlushCoalescer();
        AssertBanditBarterGold(Server, playerHeroId, banditMobilePartyId, initialPlayerGold - offeredGold, initialBanditGold + offeredGold);
        foreach (var environmentClient in Clients)
            AssertBanditBarterGold(environmentClient, playerHeroId, banditMobilePartyId, initialPlayerGold - offeredGold, initialBanditGold + offeredGold);

        Server.Call(() =>
        {
            var interactions = Server.Resolve<ICoopSessionProvider>().CoopSession.InteractionsPlayerData;
            Assert.Equal(
                (int)BanditInteractionsCampaignBehavior.PlayerInteraction.PaidOffParty,
                interactions.PlayerInteractedBandits[playerHeroId][banditMobilePartyId]);

            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(playerMobilePartyId, out var playerParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(banditMobilePartyId, out var banditParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(joiningBanditMobilePartyId, out var joiningBanditParty));
            Assert.True(DefaultMobilePartyAIModelPatches.DisablePlayerAttackTimes.TryGetValue(
                banditParty.Ai,
                out var disabledAttackTimes));
            Assert.True(disabledAttackTimes.ContainsKey(playerParty));
            Assert.True(DefaultMobilePartyAIModelPatches.DisablePlayerAttackTimes.TryGetValue(
                joiningBanditParty.Ai,
                out var joiningBanditDisabledAttackTimes));
            Assert.True(joiningBanditDisabledAttackTimes.ContainsKey(playerParty));
            Assert.Equal(AiBehavior.Hold, joiningBanditParty.DefaultBehavior);
            Assert.Null(joiningBanditParty.TargetParty);

            // HoursFromNow is stubbed to Zero by the E2E bootstrap; use a future deadline for the release assertion.
            DefaultMobilePartyAIModelPatches.PreventAttacksUntil(
                banditParty,
                playerParty,
                Campaign.Current.MapTimeTracker.Now + CampaignTime.Hours(32));
            DefaultMobilePartyAIModelPatches.PreventAttacksUntil(
                joiningBanditParty,
                playerParty,
                Campaign.Current.MapTimeTracker.Now + CampaignTime.Hours(32));
        });

        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkConversationEnded()));

        try
        {
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(playerMobilePartyId, out var playerParty));
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(banditMobilePartyId, out var banditParty));
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(joiningBanditMobilePartyId, out var joiningBanditParty));

                Assert.False(ConversationPartyHold.IsInPlayerConversation(banditParty));
                Assert.False(banditParty.Ai.IsDisabled);
                Assert.True(DefaultMobilePartyAIModelPatches.DisablePlayerAttackTimes.TryGetValue(
                    banditParty.Ai,
                    out var disabledAttackTimes));
                Assert.True(disabledAttackTimes.ContainsKey(playerParty));
                Assert.False(disabledAttackTimes[playerParty].IsPast);
                Assert.True(DefaultMobilePartyAIModelPatches.DisablePlayerAttackTimes.TryGetValue(
                    joiningBanditParty.Ai,
                    out var joiningBanditDisabledAttackTimes));
                Assert.True(joiningBanditDisabledAttackTimes.ContainsKey(playerParty));
                Assert.False(joiningBanditDisabledAttackTimes[playerParty].IsPast);
            });
        }
        finally
        {
            Server.Call(() =>
            {
                if (Server.ObjectManager.TryGetObject<MobileParty>(banditMobilePartyId, out var banditParty))
                    DefaultMobilePartyAIModelPatches.RemoveAttackProtectionsForParty(banditParty);
                if (Server.ObjectManager.TryGetObject<MobileParty>(joiningBanditMobilePartyId, out var joiningBanditParty))
                    DefaultMobilePartyAIModelPatches.RemoveAttackProtectionsForParty(joiningBanditParty);
            });
        }
    }

    [Fact]
    public void BanditSafePassage_Underpayment_IsRejectedWithoutEffects()
    {
        const int initialPlayerGold = 1000;
        const int initialBanditGold = 40;

        var client = Clients.First();
        client.Resolve<IControllerIdProvider>().SetControllerId("PlayerOne");
        var (playerHeroId, playerMobilePartyId) = CreatePlayerPartyWithRegisteredLeader("PlayerOne");
        var playerPartyId = GetPartyBaseId(Server, playerMobilePartyId);
        var banditMobilePartyId = CreateBanditParty();

        Server.Resolve<IPlayerManager>().SetPeer("PlayerOne", client.NetPeer);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(playerHeroId, out var playerHero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(playerMobilePartyId, out var playerParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(banditMobilePartyId, out var banditParty));

            Server.Resolve<ISessionInteractionsPlayerDataInterface>().AddPlayerKeys(playerHeroId);
            playerHero.Gold = initialPlayerGold;
            banditParty.PartyTradeGold = initialBanditGold;
            banditParty.SetMoveEngageParty(playerParty, MobileParty.NavigationType.Default);
            EncounterManager.StartPartyEncounter(banditParty.Party, playerParty.Party);
        });
        TestEnvironment.FlushCoalescer();

        var allowed = Server.NetworkSentMessages.GetMessages<NetworkAllowConversation>().Single();
        Assert.Equal(GetPartyBaseId(Server, banditMobilePartyId), allowed.AttackerId);
        Assert.Equal(playerPartyId, allowed.DefenderId);

        Server.NetworkSentMessages.Clear();
        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkRequestBanditBarter(
            banditMobilePartyId,
            playerGold: 1,
            Array.Empty<ItemRosterElementData>(),
            Array.Empty<TroopRosterElementData>(),
            "bandit-underpayment")));

        var result = Server.NetworkSentMessages.GetMessages<NetworkBanditBarterResult>().Single();
        Assert.False(result.Accepted);
        Assert.Equal("bandit-underpayment", result.RequestId);
        Assert.Equal(initialPlayerGold, result.PlayerGold);

        TestEnvironment.FlushCoalescer();
        AssertBanditBarterGold(Server, playerHeroId, banditMobilePartyId, initialPlayerGold, initialBanditGold);
        foreach (var environmentClient in Clients)
            AssertBanditBarterGold(environmentClient, playerHeroId, banditMobilePartyId, initialPlayerGold, initialBanditGold);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(playerMobilePartyId, out var playerParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(banditMobilePartyId, out var banditParty));
            var interactions = Server.Resolve<ICoopSessionProvider>().CoopSession.InteractionsPlayerData;

            Assert.DoesNotContain(banditMobilePartyId, interactions.PlayerInteractedBandits[playerHeroId].Keys);
            Assert.True(Server.Resolve<ConversationPartyTracker>().TryGetEngagement(client.NetPeer, out var engagement));
            Assert.Equal(GetPartyBaseId(Server, banditMobilePartyId), engagement.PartyId);
            Assert.True(banditParty.Ai.IsDisabled);
            Assert.False(DefaultMobilePartyAIModelPatches.DisablePlayerAttackTimes.TryGetValue(
                banditParty.Ai,
                out var disabledAttackTimes) && disabledAttackTimes.ContainsKey(playerParty));
        });

        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkConversationEnded()));
    }

    [Fact]
    public void BanditSafePassage_PlayerAttackedBandit_IsRejectedWithoutEffects()
    {
        const int initialPlayerGold = 1000;
        const int initialBanditGold = 40;

        var client = Clients.First();
        client.Resolve<IControllerIdProvider>().SetControllerId("PlayerOne");
        var (playerHeroId, playerMobilePartyId) = CreatePlayerPartyWithRegisteredLeader("PlayerOne");
        var playerPartyId = GetPartyBaseId(Server, playerMobilePartyId);
        var banditMobilePartyId = CreateBanditParty();

        Server.Resolve<IPlayerManager>().SetPeer("PlayerOne", client.NetPeer);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(playerHeroId, out var playerHero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(playerMobilePartyId, out var playerParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(banditMobilePartyId, out var banditParty));

            Server.Resolve<ISessionInteractionsPlayerDataInterface>().AddPlayerKeys(playerHeroId);
            playerHero.Gold = initialPlayerGold;
            banditParty.PartyTradeGold = initialBanditGold;
            playerParty.SetMoveEngageParty(banditParty, MobileParty.NavigationType.Default);
            EncounterManager.StartPartyEncounter(playerParty.Party, banditParty.Party);
        });
        TestEnvironment.FlushCoalescer();

        var allowed = Server.NetworkSentMessages.GetMessages<NetworkAllowConversation>().Single();
        Assert.Equal(playerPartyId, allowed.AttackerId);
        Assert.Equal(GetPartyBaseId(Server, banditMobilePartyId), allowed.DefenderId);

        Server.NetworkSentMessages.Clear();
        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkRequestBanditBarter(
            banditMobilePartyId,
            playerGold: 250,
            Array.Empty<ItemRosterElementData>(),
            Array.Empty<TroopRosterElementData>(),
            "bandit-wrong-side")));

        var result = Server.NetworkSentMessages.GetMessages<NetworkBanditBarterResult>().Single();
        Assert.False(result.Accepted);

        TestEnvironment.FlushCoalescer();
        AssertBanditBarterGold(Server, playerHeroId, banditMobilePartyId, initialPlayerGold, initialBanditGold);
        foreach (var environmentClient in Clients)
            AssertBanditBarterGold(environmentClient, playerHeroId, banditMobilePartyId, initialPlayerGold, initialBanditGold);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(playerMobilePartyId, out var playerParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(banditMobilePartyId, out var banditParty));
            var interactions = Server.Resolve<ICoopSessionProvider>().CoopSession.InteractionsPlayerData;

            Assert.DoesNotContain(banditMobilePartyId, interactions.PlayerInteractedBandits[playerHeroId].Keys);
            Assert.False(DefaultMobilePartyAIModelPatches.DisablePlayerAttackTimes.TryGetValue(
                banditParty.Ai,
                out var disabledAttackTimes) && disabledAttackTimes.ContainsKey(playerParty));
        });

        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkConversationEnded()));
    }

    [Fact]
    public void ExistingBattleJoin_WithMissionMember_UsesExistingAllowPath()
    {
        var (client1, client2, initiatorPartyId, responderPartyId) = CreateTwoPlayerParties();
        var mapEventSideId = CreateServerMapEventSide();

        Server.SimulateMessage(client2.NetPeer, new NetworkMissionEntered("PlayerTwo", "live-battle"));
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEventSide>(mapEventSideId, out var side));
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(responderPartyId, out var responderParty));

            responderParty.MapEventSide = side;
        }, MapEventDisabledMethods);

        RequestInteraction(client1, initiatorPartyId, responderPartyId);

        var allowed = Server.NetworkSentMessages.GetMessages<NetworkAllowConversation>().Single();
        Assert.Equal(initiatorPartyId, allowed.AttackerId);
        Assert.Equal(responderPartyId, allowed.DefenderId);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkConversationDenied>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionStarted>());
    }

    [Fact]
    public void PlayerAwaitingBattleMissionExit_BlocksEncountersUntilMissionLeft()
    {
        var (_, client2, _, responderPartyId) = CreateTwoPlayerParties();
        var aiPartyId = CreateMobilePartyBase();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        const string concludedBattleId = "concluded-battle";

        Server.SimulateMessage(client2.NetPeer, new NetworkMissionEntered("PlayerTwo", "stale-battle"));
        Server.SimulateMessage(client2.NetPeer, new NetworkMissionEntered("PlayerTwo", concludedBattleId));
        Server.SimulateMessage(client2.NetPeer, new NetworkMissionLeft("PlayerTwo", "stale-battle"));
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(aiPartyId, out var aiParty));
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(responderPartyId, out var responderParty));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.False(InvokeEncounterPrefix("StartPartyEncounterPrefix", aiParty, responderParty));
            Assert.False(InvokeEncounterPrefix("Prefix", responderParty.MobileParty, settlement));
        });

        RequestInteraction(client2, responderPartyId, aiPartyId);

        var denied = Server.NetworkSentMessages.GetMessages<NetworkConversationDenied>().Single();
        Assert.Equal(ConversationDeniedReason.PlayerUnavailable, denied.Reason);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkAllowConversation>());

        Server.NetworkSentMessages.Clear();
        Server.SimulateMessage(client2.NetPeer, new NetworkMissionLeft("PlayerTwo", concludedBattleId));
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(responderPartyId, out var responderParty));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.True(InvokeEncounterPrefix("Prefix", responderParty.MobileParty, settlement));
        });
        RequestInteraction(client2, responderPartyId, aiPartyId);

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkAllowConversation>());
    }

    private (EnvironmentInstance client1, EnvironmentInstance client2, string initiatorPartyId, string responderPartyId) CreateTwoPlayerParties()
    {
        var (client1, client2, _, _, initiatorPartyId, responderPartyId) = CreateTwoPlayerPartiesWithHeroes();
        return (client1, client2, initiatorPartyId, responderPartyId);
    }

    private (EnvironmentInstance client1, EnvironmentInstance client2, string initiatorHeroId, string responderHeroId, string initiatorPartyId, string responderPartyId) CreateTwoPlayerPartiesWithHeroes()
    {
        var clients = Clients.ToArray();
        var client1 = clients[0];
        var client2 = clients[1];

        client1.Resolve<IControllerIdProvider>().SetControllerId("PlayerOne");
        client2.Resolve<IControllerIdProvider>().SetControllerId("PlayerTwo");

        var (initiatorHeroId, initiatorMobilePartyId) = CreatePlayerHeroParty("PlayerOne");
        var (responderHeroId, responderMobilePartyId) = CreatePlayerHeroParty("PlayerTwo");

        return (
            client1,
            client2,
            initiatorHeroId,
            responderHeroId,
            GetPartyBaseId(Server, initiatorMobilePartyId),
            GetPartyBaseId(Server, responderMobilePartyId));
    }

    private void SetMainParty(EnvironmentInstance instance, string partyId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<PartyBase>(partyId, out var party));
            Assert.NotNull(party.MobileParty);
            Campaign.Current.MainParty = party.MobileParty;
        }, MapEventDisabledMethods);
    }

    private (string SiegeEventId, string SettlementId, string LeaderPartyId) CreateSyncedSiege()
    {
        var siegeCreationDisabledMethods = new[]
        {
            AccessTools.Method(typeof(MobileParty), nameof(MobileParty.OnPartyJoinedSiegeInternal)),
            AccessTools.Method(typeof(BesiegerCamp), nameof(BesiegerCamp.InitializeSiegeEventSide)),
            AccessTools.Method(typeof(Settlement), nameof(Settlement.InitializeSiegeEventSide)),
        };
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var leaderMobilePartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var leaderPartyId = GetPartyBaseId(Server, leaderMobilePartyId);

        string? siegeEventId = null;
        string? campId = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(leaderMobilePartyId, out var leader));

            var siegeEvent = new SiegeEvent(settlement, leader);
            Assert.NotNull(siegeEvent.BesiegerCamp);

            Assert.True(Server.ObjectManager.TryGetId(siegeEvent, out siegeEventId));
            Assert.True(Server.ObjectManager.TryGetId(siegeEvent.BesiegerCamp, out campId));
        }, siegeCreationDisabledMethods);

        Assert.NotNull(siegeEventId);
        Assert.NotNull(campId);

        // The headless fixture suppresses OnPartyJoinedSiegeInternal, so wire the already-synced graph's
        // membership on each replica without replacing any registered siege object.
        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<SiegeEvent>(siegeEventId!, out var siegeEvent));
                Assert.True(instance.ObjectManager.TryGetObject<BesiegerCamp>(campId!, out var camp));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
                Assert.True(instance.ObjectManager.TryGetObject<PartyBase>(leaderPartyId, out var leaderParty));
                Assert.NotNull(leaderParty.MobileParty);

                using (new AllowedThread())
                {
                    leaderParty.MobileParty._besiegerCamp = camp;
                    camp._leaderParty = leaderParty.MobileParty;
                    if (!camp._besiegerParties.Contains(leaderParty.MobileParty))
                        camp._besiegerParties.Add(leaderParty.MobileParty);
                }

                Assert.Same(camp, siegeEvent.BesiegerCamp);
                Assert.Same(settlement, siegeEvent.BesiegedSettlement);
                Assert.Same(siegeEvent, camp.SiegeEvent);
                Assert.Same(leaderParty.MobileParty, camp.LeaderParty);
            }, MapEventDisabledMethods);
        }

        return (siegeEventId!, settlementId, leaderPartyId);
    }

    private PlayerEncounter PrepareClientSiegeEncounter(
        EnvironmentInstance client,
        string playerPartyId,
        string settlementId,
        bool enterSettlement = true)
    {
        SetMainParty(client, playerPartyId);
        EnableHeadlessEncounterFinish(client);

        PlayerEncounter? encounter = null;
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<PartyBase>(playerPartyId, out var playerParty));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.NotNull(playerParty.MobileParty);

            using (new AllowedThread())
            {
                if (enterSettlement)
                    playerParty.MobileParty._currentSettlement = settlement;
                else
                    playerParty.MobileParty._currentSettlement = null;
                Hero.MainHero._partyBelongedTo = playerParty.MobileParty;
            }

            if (Campaign.Current.GetCampaignBehavior<EncounterGameMenuBehavior>() == null)
            {
                Campaign.Current.AddCampaignBehaviorManager(new CampaignBehaviorManager(
                    new CampaignBehaviorBase[] { new EncounterGameMenuBehavior() }));
            }

            encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
            encounter.EncounterSettlementAux = settlement;
            Campaign.Current.PlayerEncounter = encounter;

            if (enterSettlement)
            {
                Assert.Same(settlement, Settlement.CurrentSettlement);
                Assert.Same(settlement, Hero.MainHero.CurrentSettlement);
                Assert.Same(settlement.SiegeEvent, PlayerSiege.PlayerSiegeEvent);
            }
            else
            {
                Assert.Null(playerParty.MobileParty.CurrentSettlement);
                Assert.Null(Hero.MainHero.CurrentSettlement);
                Assert.Same(settlement, PlayerEncounter.EncounterSettlement);
            }
        }, MapEventDisabledMethods);

        Assert.NotNull(encounter);
        return encounter!;
    }

    private void PrepareBreakInDefenderEligibility(
        string playerPartyId,
        string settlementId,
        string siegeLeaderPartyId)
    {
        var defenderClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var attackerClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var townId = TestEnvironment.CreateRegisteredObject<Town>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(playerPartyId, out var playerParty));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(siegeLeaderPartyId, out var siegeLeaderParty));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(defenderClanId, out var defenderClan));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(attackerClanId, out var attackerClan));
            Assert.True(Server.ObjectManager.TryGetObject<Town>(townId, out var town));
            Assert.NotNull(playerParty.MobileParty);
            Assert.NotNull(siegeLeaderParty.MobileParty);

            playerParty.LeaderHero.Clan = defenderClan;
            playerParty.MobileParty.ActualClan = defenderClan;
            siegeLeaderParty.LeaderHero.Clan = attackerClan;
            siegeLeaderParty.MobileParty.ActualClan = attackerClan;
            SetupFief(settlement, town, playerParty);
            VillageHostileFactionStanceHelper.ApplyWarStance(
                siegeLeaderParty.MapFaction,
                playerParty.MapFaction);

            Assert.True(settlement.SiegeEvent.CanPartyJoinSide(
                playerParty,
                BattleSideEnum.Defender));
        }, MapEventDisabledMethods);

        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<PartyBase>(
                    playerPartyId,
                    out var playerParty));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(
                    settlementId,
                    out var settlement));
                Assert.True(instance.ObjectManager.TryGetObject<Town>(
                    townId,
                    out var town));
                Assert.NotNull(playerParty.MobileParty);
                using (new AllowedThread())
                {
                    settlement.Town = town;
                    settlement.SetSettlementComponent(town);
                    playerParty.MobileParty._currentSettlement = null;
                }
            });
        }
    }

    private static void AssertPartyOutsideSettlement(
        EnvironmentInstance instance,
        string playerMobilePartyId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(
                playerMobilePartyId,
                out var playerParty));
            Assert.Null(playerParty.CurrentSettlement);
        });
    }

    private static void AssertPartyEnteredSettlement(
        EnvironmentInstance instance,
        string playerMobilePartyId,
        string settlementId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(
                playerMobilePartyId,
                out var playerParty));
            Assert.True(instance.ObjectManager.TryGetObject<Settlement>(
                settlementId,
                out var settlement));
            Assert.Same(settlement, playerParty.CurrentSettlement);
        });
    }

    private void MarkPartyPending(
        EnvironmentInstance client,
        string mapEventId,
        string partyId)
    {
        client.SimulateMessage(
            Server.NetPeer,
            new NetworkMapEventPartyPending(mapEventId, partyId));

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<PartyBase>(partyId, out var party));
            Assert.True(client.Resolve<IMapEventInitializationBarrier>().IsPartyPending(party));
            Assert.False(PendingMapEventPartyMovementPatch.CanAdvancePosition(party));
        });
    }

    private static MethodBase GetNetworkRoutingMethod()
    {
        var method = AccessTools.Method(
            typeof(TestNetworkRouter),
            nameof(TestNetworkRouter.SendAll),
            new[] { typeof(NetPeer), typeof(IMessage) });
        Assert.NotNull(method);
        return method;
    }

    private static MethodBase GetDirectNetworkRoutingMethod()
    {
        var method = AccessTools.Method(
            typeof(TestNetworkRouter),
            nameof(TestNetworkRouter.Send),
            new[] { typeof(NetPeer), typeof(NetPeer), typeof(IMessage) });
        Assert.NotNull(method);
        return method;
    }

    private static void ResetConversationRequestCooldown(EnvironmentInstance client)
    {
        client.Call(() =>
        {
            var handler = client.Resolve<ConversationRequestHandler>();
            var field = AccessTools.Field(
                typeof(ConversationRequestHandler),
                "lastRequestSentUtc");
            Assert.NotNull(field);
            field.SetValue(handler, DateTime.MinValue);
        });
    }

    private static string? GetPendingConversationRequestId(EnvironmentInstance client)
    {
        string? requestId = null;
        client.Call(() =>
        {
            var handler = client.Resolve<ConversationRequestHandler>();
            var field = AccessTools.Field(
                typeof(ConversationRequestHandler),
                "pendingConversationRequestId");
            Assert.NotNull(field);
            requestId = (string?)field.GetValue(handler);
        });
        return requestId;
    }

    private static string? GetActiveConversationRequestId(EnvironmentInstance client)
    {
        string? requestId = null;
        client.Call(() =>
        {
            var handler = client.Resolve<ConversationRequestHandler>();
            var field = AccessTools.Field(
                typeof(ConversationRequestHandler),
                "activeConversationRequestId");
            Assert.NotNull(field);
            requestId = (string?)field.GetValue(handler);
        });
        return requestId;
    }

    private static bool ForceImmediateBattleEncounterMenu(
        ref string __result,
        ref bool startBattle,
        ref bool joinBattle)
    {
        __result = "encounter";
        startBattle = true;
        joinBattle = false;
        return false;
    }

    private static void InvokeSallyOutConsequence()
    {
        var method = AccessTools.Method(typeof(EncounterGameMenuBehavior), "sally_out_consequence");
        Assert.NotNull(method);

        var behavior = ObjectHelper.SkipConstructor<EncounterGameMenuBehavior>();
        method.Invoke(behavior, Array.Empty<object>());
    }

    private static void InvokeBreakInContinuation()
    {
        var method = AccessTools.Method(
            typeof(EncounterGameMenuBehavior),
            "break_in_debrief_continue_on_consequence");
        Assert.NotNull(method);

        var behavior = ObjectHelper.SkipConstructor<EncounterGameMenuBehavior>();
        method.Invoke(behavior, new object?[] { null });
    }

    private void AssertApprovedSiegeEncounter(
        EnvironmentInstance client,
        PlayerEncounter capturedEncounter,
        string settlementId,
        string leaderPartyId)
    {
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.True(client.ObjectManager.TryGetObject<PartyBase>(leaderPartyId, out var leaderParty));

            Assert.NotSame(capturedEncounter, PlayerEncounter.Current);
            Assert.Same(leaderParty, PlayerEncounter.EncounteredParty);
            Assert.Same(settlement, PlayerEncounter.EncounterSettlement);
            Assert.False(PendingMapEventPartyMovementPatch.CanAdvancePosition(leaderParty));
        }, MapEventDisabledMethods);
    }

    private string StartTrade(
        EnvironmentInstance client1,
        EnvironmentInstance client2,
        string initiatorPartyId,
        string responderPartyId,
        PlayerPartyInteractionOption tradeOption = PlayerPartyInteractionOption.TradeProposal)
    {
        RequestInteraction(client1, initiatorPartyId, responderPartyId);
        var sessionId = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionStarted>().Single().SessionId;
        SubmitOption(client1, sessionId, initiatorPartyId, tradeOption);
        SubmitOption(client2, sessionId, responderPartyId, PlayerPartyInteractionOption.AcceptProposal);

        return sessionId;
    }

    private string CreateMobilePartyBase()
    {
        var mobilePartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        return GetPartyBaseId(Server, mobilePartyId);
    }

    private (string heroId, string mobilePartyId) CreatePlayerPartyWithRegisteredLeader(string controllerId)
    {
        var mobilePartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        string? heroId = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var party));
            Assert.NotNull(party.LeaderHero);
            Assert.True(Server.ObjectManager.TryGetId(party.LeaderHero, out heroId));
        });

        Assert.NotNull(heroId);
        RegisterAsPlayerParty(controllerId, heroId!, mobilePartyId);
        return (heroId!, mobilePartyId);
    }

    private (string heroId, string mobilePartyId, string partyId) CreateAiPartyWithRegisteredLeader()
    {
        var mobilePartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        string? heroId = null;
        string? partyId = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var party));
            Assert.NotNull(party.LeaderHero);
            Assert.True(Server.ObjectManager.TryGetId(party.LeaderHero, out heroId));
            Assert.True(Server.ObjectManager.TryGetId(party.Party, out partyId));
        });

        Assert.NotNull(heroId);
        Assert.NotNull(partyId);
        return (heroId!, mobilePartyId, partyId!);
    }

    private string CreateBanditParty(string stringId = "E2EBanditSafePassage")
    {
        string? banditPartyId = null;

        Server.Call(() =>
        {
            var clan = GameObjectCreator.CreateInitializedObject<Clan>();
            var hideout = GameObjectCreator.CreateInitializedObject<Hideout>();
            var banditParty = BanditPartyComponent.CreateBanditParty(
                stringId,
                clan,
                hideout,
                isBossParty: false,
                pt: null,
                new CampaignVec2(Vec2.Zero, true));

            Assert.True(Server.ObjectManager.TryGetId(banditParty, out banditPartyId));
            Assert.True(banditParty.IsBandit);
        });

        Assert.NotNull(banditPartyId);
        return banditPartyId!;
    }

    private static void AssertBanditBarterGold(
        EnvironmentInstance instance,
        string playerHeroId,
        string banditPartyId,
        int expectedPlayerGold,
        int expectedBanditGold)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(playerHeroId, out var playerHero));
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(banditPartyId, out var banditParty));
            Assert.Equal(expectedPlayerGold, playerHero.Gold);
            Assert.Equal(expectedBanditGold, banditParty.PartyTradeGold);
        });
    }

    private void PreparePlayerPartyForCapture(string heroId, string partyId)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(partyId, out var party));

            using (new AllowedThread())
            {
                party.MobileParty.MemberRoster.AddToCounts(hero.CharacterObject, 1);
                hero.PartyBelongedTo = party.MobileParty;
                party.MobileParty.ChangePartyLeader(hero);
            }
        });
    }

    private static string GetMobilePartyId(EnvironmentInstance instance, string partyId)
    {
        string? mobilePartyId = null;

        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<PartyBase>(partyId, out var party));
            Assert.NotNull(party.MobileParty);
            Assert.True(instance.ObjectManager.TryGetId(party.MobileParty, out mobilePartyId));
        });

        Assert.NotNull(mobilePartyId);
        return mobilePartyId!;
    }

    private IReadOnlyList<MethodBase> HostileDemandSurrenderDisabledMethods()
        => MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(DefaultBattleRewardModel), nameof(DefaultBattleRewardModel.GetCaptureMemberChancesForWinnerParties)))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyCasualties"))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyItems"))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyPrisoners"))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyShips"))
            .Append(AccessTools.Method(typeof(MapEvent), "CalculateMapEventResults"))
            .Append(AccessTools.Method(typeof(MapEvent), "CommitCalculatedMapEventResults"))
            .ToList();

    private IReadOnlyList<MethodBase> HostileEncounterFinalizeDisabledMethods()
        => MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ExitToLast)))
            .Append(AccessTools.Method(typeof(MobileParty), nameof(MobileParty.TeleportPartyToOutSideOfEncounterRadius)))
            .ToList();

    private void AssertHostileEncounterTornDown(EnvironmentInstance instance, string partyId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<PartyBase>(partyId, out var party));
            Assert.Null(party.MapEventSide);
            Assert.Null(party.MapEvent);
            Assert.Null(PlayerEncounter.Current);
        }, MapEventDisabledMethods);
    }

    private void AssertCapturedPlayerPartyParked(EnvironmentInstance instance, string partyId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<PartyBase>(partyId, out var party));
            Assert.Null(party.MapEventSide);
            Assert.Null(party.MapEvent);
            Assert.NotNull(party.MobileParty);
            Assert.False(party.MobileParty.IsActive);
            Assert.Null(party.LeaderHero);
            Assert.Equal(0, party.MemberRoster.TotalManCount);
            Assert.Equal(0, party.PrisonRoster.TotalManCount);
            Assert.Null(PlayerEncounter.Current);
        }, MapEventDisabledMethods);
    }

    private void RequestInteraction(
        EnvironmentInstance client,
        string initiatorPartyId,
        string responderPartyId,
        IReadOnlyList<MethodBase>? disabledMethods = null)
    {
        client.Call(() =>
            client.Resolve<INetwork>().SendAll(new NetworkRequestConversation(
                responderPartyId,
                initiatorPartyId,
                forcePlayerOutFromSettlement: false,
                ConversationRestartSource.PlayerEncounter,
                false,
                requestId: "e2e-conversation-request")),
            disabledMethods);
    }

    private static string CaptureConversationRestart(EnvironmentInstance client)
    {
        string? requestId = null;
        client.Call(() =>
            requestId = client.Resolve<IConversationRestartContextTracker>().Capture(PlayerEncounter.Current));

        Assert.NotNull(requestId);
        return requestId!;
    }

    private static MapEvent? InvokePatchedStartBattleInternal(PlayerEncounter encounter)
    {
        var method = AccessTools.Method(typeof(PlayerEncounter), "StartBattleInternal");
        Assert.NotNull(method);
        return (MapEvent?)method.Invoke(encounter, new object[method.GetParameters().Length]);
    }

    private static void InvokePatchedEncounterAttack()
    {
        var method = AccessTools.Method(typeof(MenuHelper), nameof(MenuHelper.EncounterAttackConsequence));
        Assert.NotNull(method);
        method.Invoke(null, new object[method.GetParameters().Length]);
    }

    private void DeliverConversationApproval(
        EnvironmentInstance client,
        string playerPartyId,
        string targetPartyId,
        string requestId,
        ConversationRestartSource source,
        bool forcePlayerOutFromSettlement,
        params MethodBase[] additionalDisabledMethods)
    {
        var disabledMethods = MapEventDisabledMethods
            .Concat(additionalDisabledMethods)
            .ToList();

        client.Call(() =>
            client.SimulateMessage(Server.NetPeer, new NetworkAllowConversation(
                targetPartyId,
                playerPartyId,
                forcePlayerOutFromSettlement,
                source,
                requestId)),
            disabledMethods);
    }

    private static bool InvokeEncounterPrefix(string methodName, params object[] arguments)
    {
        var patchType = AccessTools.TypeByName("GameInterface.Services.MapEvents.Patches.EncounterManagerPatches");
        var prefix = AccessTools.Method(patchType, methodName);
        Assert.NotNull(prefix);

        return (bool)prefix.Invoke(null, arguments)!;
    }

    private void PublishConversationRequest(
        EnvironmentInstance client,
        string initiatorPartyId,
        string responderPartyId,
        IReadOnlyList<MethodBase>? disabledMethods = null)
    {
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<PartyBase>(initiatorPartyId, out var initiatorParty));
            Assert.True(client.ObjectManager.TryGetObject<PartyBase>(responderPartyId, out var responderParty));

            client.Resolve<IMessageBroker>().Publish(this, new ConversationRequested(
                responderParty,
                initiatorParty,
                forcePlayerOutFromSettlement: false,
                ConversationRestartSource.PlayerEncounter, 
                armyTalkEncounter: true));
        }, disabledMethods);
    }

    private void SubmitOption(
        EnvironmentInstance client,
        string sessionId,
        string partyId,
        PlayerPartyInteractionOption option,
        IReadOnlyList<MethodBase>? disabledMethods = null)
    {
        client.Call(() =>
            client.Resolve<INetwork>().SendAll(new NetworkSubmitPlayerPartyInteractionOption(sessionId, option, partyId)),
            disabledMethods);
    }

    private void SubmitDialogOption(
        EnvironmentInstance client,
        NetworkPlayerPartyInteractionState state,
        PlayerPartyInteractionOption option)
    {
        client.Call(() =>
        {
            PlayerPartyInteractionDialogState.Apply(state);
            PlayerPartyInteractionDialogState.Submit(option);
        });
    }

    private void OpenServiceOptions(
        EnvironmentInstance client,
        NetworkPlayerPartyInteractionState state)
    {
        client.Call(() =>
        {
            PlayerPartyInteractionDialogState.Apply(state);
            PlayerPartyInteractionDialogState.ShowServiceOptions();
        });
    }

    private void SubmitCurrentDialogOption(EnvironmentInstance client, PlayerPartyInteractionOption option)
    {
        client.Call(() => PlayerPartyInteractionDialogState.Submit(option));
    }

    private static void AssertInteractionStateCleared(EnvironmentInstance client)
    {
        client.Call(() =>
        {
            Assert.False(PlayerPartyInteractionDialogState.HasActiveState);
            Assert.False(PlayerPartyTradeContext.IsActive);
        });
    }

    private void ClearPlayerPartyInteractionState()
    {
        Server.Call(ClearPlayerPartyInteractionStateForInstance);

        foreach (var client in Clients)
            client.Call(ClearPlayerPartyInteractionStateForInstance);
    }

    private static void ClearPlayerPartyInteractionStateForInstance()
    {
        PlayerPartyInteractionDialogState.Clear();
        PlayerPartyTradeContext.End();
    }

    private static string GetPartyBaseId(EnvironmentInstance instance, string mobilePartyId)
    {
        string? partyBaseId = null;

        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var party));
            Assert.True(instance.ObjectManager.TryGetId(party.Party, out partyBaseId));
        });

        Assert.NotNull(partyBaseId);
        return partyBaseId!;
    }

    private static int GetItemAmount(PartyBase party, ItemObject itemObject)
    {
        foreach (var item in party.ItemRoster)
        {
            if (item.EquipmentElement.Item == itemObject)
                return item.Amount;
        }

        return 0;
    }

    private static void AssertPartyItemSnapshotContains(ItemRosterElementData[] items, string itemId, int amount)
    {
        var item = Assert.Single(items, i => i.ItemObjectData.ItemObjectId == itemId);

        Assert.Equal(amount, item.Amount);
    }

    private static void SetupFief(Settlement settlement, Town town, PartyBase ownerParty)
    {
        settlement.Town = town;
        settlement.SetSettlementComponent(town);
        town.OwnerClan = ownerParty.LeaderHero.Clan;
        town.IsOwnerUnassigned = false;
    }

    private void SetupResponderKingdomLeader(string initiatorPartyId, string responderPartyId, int initiatorClanTier)
    {
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(initiatorPartyId, out var initiatorParty));
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(responderPartyId, out var responderParty));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));

            initiatorParty.LeaderHero.Clan.Tier = initiatorClanTier;
            responderParty.LeaderHero.Clan.Kingdom = kingdom;
            kingdom.RulingClan = responderParty.LeaderHero.Clan;
            Assert.True(responderParty.LeaderHero.IsKingdomLeader);
        });
    }

    private (string initiatorClanId, string responderClanId) AssignDistinctClans(string initiatorPartyId, string responderPartyId)
    {
        var initiatorClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var responderClanId = TestEnvironment.CreateRegisteredObject<Clan>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(initiatorPartyId, out var initiatorParty));
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(responderPartyId, out var responderParty));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(initiatorClanId, out var initiatorClan));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(responderClanId, out var responderClan));

            initiatorParty.LeaderHero.Clan = initiatorClan;
            responderParty.LeaderHero.Clan = responderClan;
            initiatorParty.MobileParty.ActualClan = initiatorClan;
            responderParty.MobileParty.ActualClan = responderClan;
            Assert.Equal(initiatorClan, initiatorParty.MapFaction);
            Assert.Equal(responderClan, responderParty.MapFaction);
        });

        return (initiatorClanId, responderClanId);
    }

    private string AssignSameClan(string initiatorPartyId, string responderPartyId)
    {
        var clanId = TestEnvironment.CreateRegisteredObject<Clan>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(initiatorPartyId, out var initiatorParty));
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(responderPartyId, out var responderParty));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(clanId, out var clan));

            initiatorParty.LeaderHero.Clan = clan;
            responderParty.LeaderHero.Clan = clan;
            initiatorParty.MobileParty.ActualClan = clan;
            responderParty.MobileParty.ActualClan = clan;
            Assert.Equal(clan, initiatorParty.MapFaction);
            Assert.Equal(clan, responderParty.MapFaction);
        });

        return clanId;
    }

    private void AssertWarDeclared(EnvironmentInstance instance, string initiatorFactionId, string responderFactionId)
    {
        instance.Call(() =>
        {
            Assert.True(TryGetFaction(instance, initiatorFactionId, out var initiatorFaction));
            Assert.True(TryGetFaction(instance, responderFactionId, out var responderFaction));
            var initiatorMapFaction = initiatorFaction!.MapFaction;
            var responderMapFaction = responderFaction!.MapFaction;
            Assert.NotNull(initiatorMapFaction);
            Assert.NotNull(responderMapFaction);
            Assert.True(
                VillageHostileFactionStanceHelper.HasWarStance(initiatorMapFaction, responderMapFaction),
                $"Expected {GetFactionDebugName(instance, initiatorMapFaction)} to be at war with {GetFactionDebugName(instance, responderMapFaction)}. InitiatorWarsContainsResponder={initiatorMapFaction.FactionsAtWarWith?.Contains(responderMapFaction) == true}, ResponderWarsContainsInitiator={responderMapFaction.FactionsAtWarWith?.Contains(initiatorMapFaction) == true}");
        });
    }

    private static void AssertWarListsContainEachOther(
        EnvironmentInstance instance,
        string initiatorFactionId,
        string responderFactionId)
    {
        instance.Call(() =>
        {
            Assert.True(TryGetFaction(instance, initiatorFactionId, out var initiatorFaction));
            Assert.True(TryGetFaction(instance, responderFactionId, out var responderFaction));
            Assert.Contains(responderFaction!.MapFaction, initiatorFaction!.MapFaction.FactionsAtWarWith);
            Assert.Contains(initiatorFaction.MapFaction, responderFaction.MapFaction.FactionsAtWarWith);
        });
    }

    private void AssertPeaceMade(EnvironmentInstance instance, string initiatorFactionId, string responderFactionId)
    {
        instance.Call(() =>
        {
            Assert.True(TryGetFaction(instance, initiatorFactionId, out var initiatorFaction));
            Assert.True(TryGetFaction(instance, responderFactionId, out var responderFaction));
            var initiatorMapFaction = initiatorFaction!.MapFaction;
            var responderMapFaction = responderFaction!.MapFaction;
            Assert.NotNull(initiatorMapFaction);
            Assert.NotNull(responderMapFaction);
            Assert.False(
                PlayerPartyPeaceBarterable.AreHostile(initiatorMapFaction, responderMapFaction),
                $"Expected {GetFactionDebugName(instance, initiatorMapFaction)} to be at peace with {GetFactionDebugName(instance, responderMapFaction)}. InitiatorWarsContainsResponder={initiatorMapFaction.FactionsAtWarWith?.Contains(responderMapFaction) == true}, ResponderWarsContainsInitiator={responderMapFaction.FactionsAtWarWith?.Contains(initiatorMapFaction) == true}");
        });
    }

    private void AssertPeaceBarterablesAvailable(string initiatorPartyId, string responderPartyId)
    {
        Server.Call(() =>
        {
            var peaceBarterables = GetPeaceBarterables(initiatorPartyId, responderPartyId);

            Assert.Equal(2, peaceBarterables.Length);
            Assert.Contains(peaceBarterables, barterable =>
                Server.ObjectManager.TryGetId(barterable.OriginalParty, out var partyId) &&
                partyId == initiatorPartyId);
            Assert.Contains(peaceBarterables, barterable =>
                Server.ObjectManager.TryGetId(barterable.OriginalParty, out var partyId) &&
                partyId == responderPartyId);
            Assert.All(peaceBarterables, barterable => Assert.Equal("Peace", barterable.Name.ToString()));
        });
    }

    private void AssertPeaceBarterablesUnavailable(string initiatorPartyId, string responderPartyId)
    {
        Server.Call(() => Assert.Empty(GetPeaceBarterables(initiatorPartyId, responderPartyId)));
    }

    private PlayerPartyPeaceBarterable[] GetPeaceBarterables(string initiatorPartyId, string responderPartyId)
    {
        Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(initiatorPartyId, out var initiatorParty));
        Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(responderPartyId, out var responderParty));

        var barterData = new BarterData(
            initiatorParty.LeaderHero,
            responderParty.LeaderHero,
            initiatorParty,
            responderParty,
            null,
            0,
            false);

        InvokeAddBarterGroups(barterData);
        InvokeAddPartyBarterables(barterData, initiatorParty.LeaderHero, responderParty.LeaderHero, initiatorParty, responderParty);
        InvokeAddPartyBarterables(barterData, responderParty.LeaderHero, initiatorParty.LeaderHero, responderParty, initiatorParty);

        return barterData.GetBarterables().OfType<PlayerPartyPeaceBarterable>().ToArray();
    }

    private void ReplaceResponderClanLeader(string responderPartyId)
    {
        var replacementLeaderId = TestEnvironment.CreateRegisteredObject<Hero>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(responderPartyId, out var responderParty));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(replacementLeaderId, out var replacementLeader));
            var responderClan = responderParty.LeaderHero.Clan;

            replacementLeader.Clan = responderClan;
            responderClan.SetLeader(replacementLeader);
            Assert.NotEqual(responderParty.LeaderHero, responderClan.Leader);
        });
    }
    private void AssertHostileEncounterMapEvent(
        EnvironmentInstance instance,
        string mapEventId,
        string initiatorPartyId,
        string responderPartyId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.True(instance.ObjectManager.TryGetObject<PartyBase>(initiatorPartyId, out var initiatorParty));
            Assert.True(instance.ObjectManager.TryGetObject<PartyBase>(responderPartyId, out var responderParty));

            Assert.Same(mapEvent, initiatorParty.MapEvent);
            Assert.Same(mapEvent, responderParty.MapEvent);
            Assert.Same(mapEvent.AttackerSide, initiatorParty.MapEventSide);
            Assert.Same(mapEvent.DefenderSide, responderParty.MapEventSide);
            Assert.Contains(mapEvent.AttackerSide.Parties, party => party.Party == initiatorParty);
            Assert.Contains(mapEvent.DefenderSide.Parties, party => party.Party == responderParty);

            if (initiatorParty.MobileParty?.IsControlledByThisInstance() == true ||
                responderParty.MobileParty?.IsControlledByThisInstance() == true)
            {
                var expectedSide = initiatorParty.MobileParty?.IsControlledByThisInstance() == true
                    ? BattleSideEnum.Attacker
                    : BattleSideEnum.Defender;
                var expectedOpponentSide = expectedSide == BattleSideEnum.Attacker
                    ? BattleSideEnum.Defender
                    : BattleSideEnum.Attacker;

                var expectedEncounteredParty = expectedSide == BattleSideEnum.Attacker
                    ? responderParty
                    : initiatorParty;

                Assert.NotNull(PlayerEncounter.Current);
                Assert.Same(mapEvent, PlayerEncounter.Battle);
                Assert.Same(initiatorParty, PlayerEncounter.Current._attackerParty);
                Assert.Same(responderParty, PlayerEncounter.Current._defenderParty);
                Assert.Same(expectedEncounteredParty, PlayerEncounter.EncounteredParty);
                Assert.Equal(expectedSide, PlayerEncounter.Current.PlayerSide);
                Assert.Equal(expectedOpponentSide, PlayerEncounter.Current.OpponentSide);
                Assert.True(PlayerEncounter.Current.IsJoinedBattle);
            }
        }, MapEventDisabledMethods);
    }

    private static string GetFactionDebugName(EnvironmentInstance instance, IFaction faction)
    {
        if (instance.ObjectManager.TryGetId(faction, out var factionId))
            return $"{faction.GetType().Name}:{factionId}";

        return faction.GetType().Name;
    }

    private static bool TryGetFaction(EnvironmentInstance instance, string factionId, out IFaction? faction)
    {
        if (instance.ObjectManager.TryGetObject<Kingdom>(factionId, out var kingdom))
        {
            faction = kingdom;
            return true;
        }

        if (instance.ObjectManager.TryGetObject<Clan>(factionId, out var clan))
        {
            faction = clan;
            return true;
        }

        faction = null;
        return false;
    }

    private (string initiatorClanId, string responderClanId) MakePartiesHostile(string initiatorPartyId, string responderPartyId)
    {
        var initiatorClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var responderClanId = TestEnvironment.CreateRegisteredObject<Clan>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(initiatorPartyId, out var initiatorParty));
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(responderPartyId, out var responderParty));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(initiatorClanId, out var initiatorClan));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(responderClanId, out var responderClan));

            initiatorParty.LeaderHero.Clan = initiatorClan;
            responderParty.LeaderHero.Clan = responderClan;
            initiatorClan.SetLeader(initiatorParty.LeaderHero);
            responderClan.SetLeader(responderParty.LeaderHero);
            initiatorParty.MobileParty.ActualClan = initiatorClan;
            responderParty.MobileParty.ActualClan = responderClan;
            VillageHostileFactionStanceHelper.ApplyWarStance(initiatorClan, responderClan);
            Assert.Equal(initiatorClan, initiatorParty.MapFaction);
            Assert.Equal(responderClan, responderParty.MapFaction);
            Assert.Contains(responderClan, initiatorClan.FactionsAtWarWith);
            Assert.Contains(initiatorClan, responderClan.FactionsAtWarWith);
        });

        return (initiatorClanId, responderClanId);
    }

    private static void InvokeAddBarterGroups(BarterData barterData)
        => typeof(PlayerPartyInteractionHandler)
            .GetMethod("AddBarterGroups", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object[] { barterData });

    private static void InvokeAddPartyBarterables(
        BarterData barterData,
        Hero ownerHero,
        Hero otherHero,
        PartyBase ownerParty,
        PartyBase otherParty)
        => typeof(PlayerPartyInteractionHandler)
            .GetMethod("AddPartyBarterables", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object[] { barterData, ownerHero, otherHero, ownerParty, otherParty });
}
