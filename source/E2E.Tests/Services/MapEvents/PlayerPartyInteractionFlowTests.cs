using Common.Network;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Entity;
using GameInterface.Services.Inventory.Data;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.MapEvents.PlayerPartyInteractions;
using GameInterface.Services.TroopRosters.Data;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
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
    public void OfferServices_WithNoValidServiceOptions_ShowsNevermindAndCanEndInteraction()
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

        Server.NetworkSentMessages.Clear();
        client1.NetworkSentMessages.Clear();
        OpenServiceOptions(client1, initialState);

        Assert.Empty(client1.NetworkSentMessages.GetMessages<NetworkSubmitPlayerPartyInteractionOption>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionState>());
        client1.Call(() =>
        {
            Assert.Equal(PlayerPartyInteractionPhase.OfferServices, PlayerPartyInteractionDialogState.Phase);
            Assert.True(PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.Leave));
            Assert.False(PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.JoinClan));
            Assert.False(PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.Vassal));
        });

        Server.NetworkSentMessages.Clear();
        SubmitCurrentDialogOption(client1, PlayerPartyInteractionOption.Leave);

        var ended = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionEnded>().Single();
        Assert.Equal(PlayerPartyInteractionOutcomeType.Left, ended.OutcomeType);
    }

    [Fact]
    public void OfferServices_WithValidServiceOptions_StillShowsNevermind()
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
            Assert.True(PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.Leave));
            Assert.False(PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.Vassal));
        });
    }

    [Fact]
    public void OfferServices_WithTierOneClanAndKingdomLeader_DoesNotShowMercenary()
    {
        var (client1, _, initiatorPartyId, responderPartyId) = CreateTwoPlayerParties();
        SetupResponderKingdomLeader(initiatorPartyId, responderPartyId, initiatorClanTier: 1);

        RequestInteraction(client1, initiatorPartyId, responderPartyId);
        var sessionId = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionStarted>().Single().SessionId;
        var initialState = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionState>().Single(s =>
            s.SessionId == sessionId &&
            s.PartyId == initiatorPartyId &&
            s.Phase == PlayerPartyInteractionPhase.InitialOptions);

        OpenServiceOptions(client1, initialState);

        client1.Call(() =>
        {
            Assert.True(PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.JoinClan));
            Assert.True(PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.Leave));
            Assert.False(PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.Vassal));
        });
    }

    [Fact]
    public void OfferServices_WithTierTwoClanAndKingdomLeader_ShowsVassal()
    {
        var (client1, _, initiatorPartyId, responderPartyId) = CreateTwoPlayerParties();
        SetupResponderKingdomLeader(initiatorPartyId, responderPartyId, initiatorClanTier: 2);

        RequestInteraction(client1, initiatorPartyId, responderPartyId);
        var sessionId = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionStarted>().Single().SessionId;
        var initialState = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionState>().Single(s =>
            s.SessionId == sessionId &&
            s.PartyId == initiatorPartyId &&
            s.Phase == PlayerPartyInteractionPhase.InitialOptions);

        OpenServiceOptions(client1, initialState);

        client1.Call(() =>
        {
            Assert.True(PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.JoinClan));
            Assert.True(PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.Leave));
            Assert.True(PlayerPartyInteractionDialogState.HasOption(PlayerPartyInteractionOption.Vassal));
        });
    }

    [Fact]
    public void ClanServiceProposal_AcceptedByResponder_JoinsResponderClan()
    {
        var (client1, client2, initiatorPartyId, responderPartyId) = CreateTwoPlayerParties();

        RequestInteraction(client1, initiatorPartyId, responderPartyId);
        var sessionId = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionStarted>().Single().SessionId;
        var initialState = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionState>().Single(s =>
            s.SessionId == sessionId &&
            s.PartyId == initiatorPartyId &&
            s.Phase == PlayerPartyInteractionPhase.InitialOptions);

        OpenServiceOptions(client1, initialState);
        SubmitCurrentDialogOption(client1, PlayerPartyInteractionOption.JoinClan);
        SubmitOption(client2, sessionId, responderPartyId, PlayerPartyInteractionOption.AcceptProposal);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(initiatorPartyId, out var initiatorParty));
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(responderPartyId, out var responderParty));

            Assert.Equal(responderParty.LeaderHero.Clan, initiatorParty.LeaderHero.Clan);
            Assert.Equal(responderParty.LeaderHero.Clan, initiatorParty.MobileParty.ActualClan);
        });
    }

    [Fact]
    public void VassalServiceProposal_AcceptedByResponder_EndsWithoutJoiningKingdom()
    {
        var (client1, client2, initiatorPartyId, responderPartyId) = CreateTwoPlayerParties();
        SetupResponderKingdomLeader(initiatorPartyId, responderPartyId, initiatorClanTier: 2);

        RequestInteraction(client1, initiatorPartyId, responderPartyId);
        var sessionId = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionStarted>().Single().SessionId;
        var initialState = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionState>().Single(s =>
            s.SessionId == sessionId &&
            s.PartyId == initiatorPartyId &&
            s.Phase == PlayerPartyInteractionPhase.InitialOptions);
        string? initialKingdomId = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(initiatorPartyId, out var initiatorParty));
            var initialKingdom = initiatorParty.LeaderHero.Clan.Kingdom;
            initialKingdomId = initialKingdom?.StringId;
        });

        OpenServiceOptions(client1, initialState);
        SubmitCurrentDialogOption(client1, PlayerPartyInteractionOption.Vassal);
        Server.NetworkSentMessages.Clear();
        SubmitOption(client2, sessionId, responderPartyId, PlayerPartyInteractionOption.AcceptProposal);

        var ended = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionEnded>().Single();
        Assert.Equal(PlayerPartyInteractionOutcomeType.VassalAccepted, ended.OutcomeType);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(initiatorPartyId, out var initiatorParty));
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(responderPartyId, out var responderParty));

            var currentKingdom = initiatorParty.LeaderHero.Clan.Kingdom;
            var currentKingdomId = currentKingdom?.StringId;

            Assert.Equal(initialKingdomId, currentKingdomId);
            Assert.NotEqual(responderParty.LeaderHero.Clan.Kingdom, currentKingdom);
            Assert.False(initiatorParty.LeaderHero.Clan.IsUnderMercenaryService);
        });
    }

    [Theory]
    [InlineData(PlayerPartyInteractionProposal.Trade, "I have a proposal that may benefit us both.")]
    [InlineData(PlayerPartyInteractionProposal.JoinClan, "I wish to offer my services in your clan.")]
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
        Assert.Single(initiatorState.PartyItems);
        Assert.Equal(initiatorItemId, initiatorState.PartyItems[0].ItemObjectData.ItemObjectId);
        Assert.Equal(3, initiatorState.PartyItems[0].Amount);
        Assert.Single(initiatorState.OtherPartyItems);
        Assert.Equal(responderItemId, initiatorState.OtherPartyItems[0].ItemObjectData.ItemObjectId);
        Assert.Equal(4, initiatorState.OtherPartyItems[0].Amount);

        var responderState = tradeStates.Single(s => s.PartyId == responderPartyId);
        Assert.Single(responderState.PartyItems);
        Assert.Equal(responderItemId, responderState.PartyItems[0].ItemObjectData.ItemObjectId);
        Assert.Equal(4, responderState.PartyItems[0].Amount);
        Assert.Single(responderState.OtherPartyItems);
        Assert.Equal(initiatorItemId, responderState.OtherPartyItems[0].ItemObjectData.ItemObjectId);
        Assert.Equal(3, responderState.OtherPartyItems[0].Amount);
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

    [Fact]
    public void TradeOfferUpdate_AfterEitherPlayerAccepts_IsIgnored()
    {
        var (client1, client2, initiatorPartyId, responderPartyId) = CreateTwoPlayerParties();
        var sessionId = StartTrade(client1, client2, initiatorPartyId, responderPartyId);

        Server.NetworkSentMessages.Clear();
        client1.Call(() => client1.Resolve<INetwork>().SendAll(new NetworkPlayerPartyTradeAcceptChanged(sessionId, accepted: true)));
        Assert.Contains(Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionState>(), s =>
            s.PartyId == initiatorPartyId &&
            s.InitiatorAcceptedTrade &&
            !s.ResponderAcceptedTrade);

        Server.NetworkSentMessages.Clear();
        client1.Call(() => client1.Resolve<INetwork>().SendAll(new NetworkPlayerPartyTradeOfferUpdated(
            sessionId,
            initiatorPartyId,
            new[]
            {
                new ItemRosterElementData(new ItemObjectData("item-1", null, itemModifierNull: true), 2)
            },
            Array.Empty<TroopRosterElementData>())));

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyTradeOfferUpdated>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionState>());
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
    public void HostilePlayerParties_RequestIsRejectedWithoutStartingInteraction()
    {
        var (client1, _, initiatorPartyId, responderPartyId) = CreateTwoPlayerParties();
        MakePartiesHostile(initiatorPartyId, responderPartyId);

        RequestInteraction(client1, initiatorPartyId, responderPartyId);

        var denied = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionDenied>().Single();
        Assert.Equal(PlayerPartyInteractionDeniedReason.Hostile, denied.Reason);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionStarted>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionState>());
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
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionStarted>());
        AssertInteractionStateCleared(client1);

        client1.Call(() => client1.Resolve<INetwork>().SendAll(new NetworkConversationEnded()));
    }

    [Fact]
    public void ExistingBattleJoin_UsesExistingAllowPath()
    {
        var (client1, _, initiatorPartyId, responderPartyId) = CreateTwoPlayerParties();
        var mapEventSideId = CreateServerMapEventSide();

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
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionStarted>());
    }

    private (EnvironmentInstance client1, EnvironmentInstance client2, string initiatorPartyId, string responderPartyId) CreateTwoPlayerParties()
    {
        var clients = Clients.ToArray();
        var client1 = clients[0];
        var client2 = clients[1];

        client1.Resolve<IControllerIdProvider>().SetControllerId("PlayerOne");
        client2.Resolve<IControllerIdProvider>().SetControllerId("PlayerTwo");

        var (_, initiatorMobilePartyId) = CreatePlayerHeroParty("PlayerOne");
        var (_, responderMobilePartyId) = CreatePlayerHeroParty("PlayerTwo");

        return (
            client1,
            client2,
            GetPartyBaseId(Server, initiatorMobilePartyId),
            GetPartyBaseId(Server, responderMobilePartyId));
    }

    private string StartTrade(EnvironmentInstance client1, EnvironmentInstance client2, string initiatorPartyId, string responderPartyId)
    {
        RequestInteraction(client1, initiatorPartyId, responderPartyId);
        var sessionId = Server.NetworkSentMessages.GetMessages<NetworkPlayerPartyInteractionStarted>().Single().SessionId;
        SubmitOption(client1, sessionId, initiatorPartyId, PlayerPartyInteractionOption.TradeProposal);
        SubmitOption(client2, sessionId, responderPartyId, PlayerPartyInteractionOption.AcceptProposal);

        return sessionId;
    }

    private string CreateMobilePartyBase()
    {
        var mobilePartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        return GetPartyBaseId(Server, mobilePartyId);
    }

    private void RequestInteraction(EnvironmentInstance client, string initiatorPartyId, string responderPartyId)
    {
        client.Call(() =>
            client.Resolve<INetwork>().SendAll(new NetworkRequestConversation(
                responderPartyId,
                initiatorPartyId,
                forcePlayerOutFromSettlement: false,
                ConversationRestartSource.PlayerEncounter)));
    }

    private void SubmitOption(EnvironmentInstance client, string sessionId, string partyId, PlayerPartyInteractionOption option)
    {
        client.Call(() =>
            client.Resolve<INetwork>().SendAll(new NetworkSubmitPlayerPartyInteractionOption(sessionId, option, partyId)));
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

    private void MakePartiesHostile(string initiatorPartyId, string responderPartyId)
    {
        var initiatorClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var responderClanId = TestEnvironment.CreateRegisteredObject<Clan>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(initiatorPartyId, out var initiatorParty));
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(responderPartyId, out var responderParty));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(initiatorClanId, out var initiatorClan));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(responderClanId, out var responderClan));

            initiatorParty.MobileParty.ActualClan = initiatorClan;
            responderParty.MobileParty.ActualClan = responderClan;
            SetWar(initiatorClan, responderClan);
            Assert.Equal(initiatorClan, initiatorParty.MapFaction);
            Assert.Equal(responderClan, responderParty.MapFaction);
            Assert.Contains(responderClan, initiatorClan.FactionsAtWarWith);
            Assert.Contains(initiatorClan, responderClan.FactionsAtWarWith);
        });
    }

    private static void SetWar(Clan initiatorClan, Clan responderClan)
    {
        var factionsAtWarWith = typeof(Clan).GetField("_factionsAtWarWith", BindingFlags.Instance | BindingFlags.NonPublic);

        var initiatorWars = new MBList<IFaction> { responderClan };
        var responderWars = new MBList<IFaction> { initiatorClan };

        factionsAtWarWith!.SetValue(initiatorClan, initiatorWars);
        factionsAtWarWith.SetValue(responderClan, responderWars);
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
