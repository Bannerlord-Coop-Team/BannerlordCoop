using Common.Messaging;
using Common.Util;
using Coop.Core.Client.Services.SiegeEvents.Messages;
using Coop.Core.Server.Services.SiegeEvents.Messages;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.SiegeEvents;
using GameInterface.Services.SiegeEvents.Messages;
using HarmonyLib;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace E2E.Tests.Services.MapEvents;

// Break in and break out flows, kept next to the other siege tests. Shares the party, siege and encounter setup of PlayerPartyInteractionFlowTests.
public partial class PlayerPartyInteractionFlowTests
{
    [Fact]
    public void BreakIn_ServerAppliesTroopLossesSoClientAndServerTotalsMatch()
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
        var troops = AddRegularTroops(playerMobilePartyId, 200);
        Server.Call(() => Campaign.Current.Models.TroopSacrificeModel = new FixedTroopSacrificeModel(40));

        client.Call(() => InvokeMenuConsequence("break_in_menu_accept_on_consequence"), MapEventDisabledMethods);

        AssertRegularTroops(playerMobilePartyId, troops - 40);
        client.Call(() =>
        {
            var behavior = Campaign.Current.GetCampaignBehavior<EncounterGameMenuBehavior>();
            Assert.Equal(40, behavior._breakInOutCasualties.TotalManCount);
        });
    }

    [Fact]
    public void BreakIn_RejectWhileTheServerAnswerIsPending_StaysInTheMenuSoThePaidLossesAreNotLost()
    {
        var (client, playerMobilePartyId, _, troops) = PrepareBreakInMenu();

        // The server pays the troops but its answer has not reached the player yet.
        using (new ReliableMessageDeliveryBlocker<NetworkBreakInResult>())
            client.Call(() => InvokeMenuConsequence("break_in_menu_accept_on_consequence"), MapEventDisabledMethods);
        AssertRegularTroops(playerMobilePartyId, troops - 40);

        client.Call(() =>
        {
            InvokeMenuConsequence("break_in_menu_reject_on_consequence");
            Assert.Equal("break_in_menu", Campaign.Current.CurrentMenuContext?.GameMenu?.StringId);
        }, MapEventDisabledMethods);

        // Accepting again resends the same request and the server reuses the paid losses.
        client.Call(() => InvokeMenuConsequence("break_in_menu_accept_on_consequence"), MapEventDisabledMethods);

        AssertRegularTroops(playerMobilePartyId, troops - 40);
        client.Call(() =>
        {
            Assert.Equal("break_in_debrief_menu", Campaign.Current.CurrentMenuContext?.GameMenu?.StringId);
            Assert.Equal(40, Campaign.Current.GetCampaignBehavior<EncounterGameMenuBehavior>()._breakInOutCasualties.TotalManCount);
            Assert.False(SiegeBreakOut.IsBreakInPending);
        });
    }

    [Fact]
    public void BreakIn_AcceptedAgainAfterTheMenuChanged_LosesTroopsOnce()
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
        var troops = AddRegularTroops(playerMobilePartyId, 200);
        Server.Call(() => Campaign.Current.Models.TroopSacrificeModel = new FixedTroopSacrificeModel(40));

        // The player leaves the menu before the answer lands, so the first reply is never used.
        using (new ReliableMessageDeliveryBlocker<NetworkBreakInResult>())
            client.Call(() => InvokeMenuConsequence("break_in_menu_accept_on_consequence"), MapEventDisabledMethods);
        AssertRegularTroops(playerMobilePartyId, troops - 40);
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(siege.SettlementId, out var settlement));
            var reopened = ObjectHelper.SkipConstructor<PlayerEncounter>();
            reopened.EncounterSettlementAux = settlement;
            Campaign.Current.PlayerEncounter = reopened;
        });

        client.Call(() => InvokeMenuConsequence("break_in_menu_accept_on_consequence"), MapEventDisabledMethods);

        AssertRegularTroops(playerMobilePartyId, troops - 40);
        client.Call(() =>
        {
            var behavior = Campaign.Current.GetCampaignBehavior<EncounterGameMenuBehavior>();
            Assert.Equal(40, behavior._breakInOutCasualties.TotalManCount);
        });
    }

    [Theory]
    [InlineData(40, false)]
    [InlineData(0, true)]
    public void BreakInContinuation_WithoutAPaidBreakIn_EntersOnlyWhenBreakingInCostsNothing(int losses, bool entered)
    {
        var (client, _, playerPartyId, _) = CreateTwoPlayerParties();
        var playerMobilePartyId = GetMobilePartyId(Server, playerPartyId);
        var siege = CreateSyncedSiege();
        TestEnvironment.ConnectRegisteredPlayer(client, "PlayerOne");
        PrepareBreakInDefenderEligibility(
            playerPartyId,
            siege.SettlementId,
            siege.LeaderPartyId);
        AddRegularTroops(playerMobilePartyId, 200);
        Server.Call(() => Campaign.Current.Models.TroopSacrificeModel = new FixedTroopSacrificeModel(losses));
        Server.NetworkSentMessages.Clear();

        Server.Call(
            () => Server.SimulateMessage(
                client.NetPeer,
                new NetworkRequestBreakInContinuation("unpaid", playerMobilePartyId, siege.SettlementId)),
            MapEventDisabledMethods);

        Assert.Equal(entered, Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkBreakInContinuationApproved>()).Approved);
        if (entered)
            AssertPartyEnteredSettlement(Server, playerMobilePartyId, siege.SettlementId);
        else
            AssertPartyOutsideSettlement(Server, playerMobilePartyId);
    }

    [Fact]
    public void BreakIn_WithTooFewRegulars_IsTurnedDownUntilTroopsArriveAndThenPaysOnce()
    {
        var (client, _, playerPartyId, _) = CreateTwoPlayerParties();
        var playerMobilePartyId = GetMobilePartyId(Server, playerPartyId);
        var siege = CreateSyncedSiege();
        TestEnvironment.ConnectRegisteredPlayer(client, "PlayerOne");
        PrepareBreakInDefenderEligibility(
            playerPartyId,
            siege.SettlementId,
            siege.LeaderPartyId);
        var troops = AddRegularTroops(playerMobilePartyId, 30);
        var required = troops + 10;
        Server.Call(() => Campaign.Current.Models.TroopSacrificeModel = new FixedTroopSacrificeModel(required));
        Server.NetworkSentMessages.Clear();

        RequestBreakIn(client, playerMobilePartyId, siege.SettlementId);
        RequestContinuation(client, playerMobilePartyId, siege.SettlementId, "unaffordable");

        Assert.False(Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBreakInResult>()).Approved);
        Assert.False(Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBreakInContinuationApproved>()).Approved);
        AssertRegularTroops(playerMobilePartyId, troops);
        AssertPartyOutsideSettlement(Server, playerMobilePartyId);

        troops = AddRegularTroops(playerMobilePartyId, 20);
        Server.NetworkSentMessages.Clear();
        RequestBreakIn(client, playerMobilePartyId, siege.SettlementId);
        RequestBreakIn(client, playerMobilePartyId, siege.SettlementId);
        RequestContinuation(client, playerMobilePartyId, siege.SettlementId, "affordable");

        Assert.All(Server.NetworkSentMessages.GetMessages<NetworkBreakInResult>(), result => Assert.True(result.Approved));
        AssertRegularTroops(playerMobilePartyId, troops - required);
        Assert.True(Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBreakInContinuationApproved>()).Approved);
        AssertPartyEnteredSettlement(Server, playerMobilePartyId, siege.SettlementId);
    }

    private void RequestBreakIn(EnvironmentInstance client, string mobilePartyId, string settlementId)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            PayBreakIn(client, party, settlement);
        }, MapEventDisabledMethods);
        TestEnvironment.FlushCoalescer();
    }

    private void RequestContinuation(EnvironmentInstance client, string mobilePartyId, string settlementId, string requestId)
    {
        Server.Call(
            () => Server.SimulateMessage(
                client.NetPeer,
                new NetworkRequestBreakInContinuation(requestId, mobilePartyId, settlementId)),
            MapEventDisabledMethods);
    }

    [Fact]
    public void PortBreakOut_ServerAppliesTroopLossesSoClientAndServerTotalsMatch()
    {
        var (client, playerMobilePartyId, troops) = RequestPortBreakOut(hasPort: true, hasShips: true, anchoredHere: true);

        Assert.True(Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBreakOutResult>()).Approved);
        AssertRegularTroops(playerMobilePartyId, troops - 30);
        client.Call(() =>
        {
            var behavior = Campaign.Current.GetCampaignBehavior<EncounterGameMenuBehavior>();
            Assert.Equal(30, behavior._breakInOutCasualties.TotalManCount);
            Assert.True(behavior._isBreakingOutFromPort);
        });
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void PortBreakOut_WithoutAPortShipsOrTheFleetAnchoredHere_IsTurnedDownWithoutLosses(
        bool hasPort, bool hasShips, bool anchoredHere)
    {
        var (_, playerMobilePartyId, troops) = RequestPortBreakOut(hasPort, hasShips, anchoredHere);

        Assert.False(Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBreakOutResult>()).Approved);
        AssertRegularTroops(playerMobilePartyId, troops);
    }

    [Fact]
    public void BreakIn_ArmyLeaderWhoseOwnPartyLosesNobody_StillGetsTheDebriefAndEnters()
    {
        var (client, _, playerPartyId, otherPartyId) = CreateTwoPlayerParties();
        var playerMobilePartyId = GetMobilePartyId(Server, playerPartyId);
        var otherMobilePartyId = GetMobilePartyId(Server, otherPartyId);
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
        var armyTroops = AddRegularTroops(otherMobilePartyId, 200);
        LeadArmyWithTroopsInAnotherParty(playerMobilePartyId, otherMobilePartyId);
        Server.Call(() => Campaign.Current.Models.TroopSacrificeModel = new FixedTroopSacrificeModel(40));
        OpenBreakInMenu(client);
        Server.NetworkSentMessages.Clear();

        client.Call(() => InvokeMenuConsequence("break_in_menu_accept_on_consequence"), MapEventDisabledMethods);

        var result = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBreakInResult>());
        Assert.True(result.Approved);
        Assert.Empty(result.CharacterIds);
        AssertRegularTroops(otherMobilePartyId, armyTroops - 40);
        client.Call(() =>
        {
            Assert.Equal("break_in_debrief_menu", Campaign.Current.CurrentMenuContext?.GameMenu?.StringId);
            var behavior = Campaign.Current.GetCampaignBehavior<EncounterGameMenuBehavior>();
            Assert.Equal(0, behavior._breakInOutCasualties.TotalManCount);
            Assert.Equal(40, behavior._breakInOutArmyCasualties);
            Assert.False(SiegeBreakOut.IsBreakInPending);
        });

        RequestContinuation(client, playerMobilePartyId, siege.SettlementId, "army-leader");

        Assert.True(Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBreakInContinuationApproved>()).Approved);
        AssertPartyEnteredSettlement(Server, playerMobilePartyId, siege.SettlementId);
    }

    [Fact]
    public void GateBreakOut_ArmyLeaderWhoseOwnPartyLosesNobody_StillGetsTheDebrief()
    {
        var (client, _, playerPartyId, otherPartyId) = CreateTwoPlayerParties();
        var playerMobilePartyId = GetMobilePartyId(Server, playerPartyId);
        var otherMobilePartyId = GetMobilePartyId(Server, otherPartyId);
        var siege = CreateSyncedSiege();
        TestEnvironment.ConnectRegisteredPlayer(client, "PlayerOne");
        PrepareClientSiegeEncounter(client, playerPartyId, siege.SettlementId);
        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(playerMobilePartyId, out var party));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(siege.SettlementId, out var settlement));
                using (new AllowedThread())
                    party._currentSettlement = settlement;
            });
        }
        var armyTroops = AddRegularTroops(otherMobilePartyId, 200);
        LeadArmyWithTroopsInAnotherParty(playerMobilePartyId, otherMobilePartyId);
        Server.Call(() => Campaign.Current.Models.TroopSacrificeModel = new FixedTroopSacrificeModel(0, gateLosses: 30));
        Server.NetworkSentMessages.Clear();

        client.Call(() =>
        {
            Campaign.Current.GetCampaignBehavior<EncounterGameMenuBehavior>()._isBreakingOutFromPort = false;
            InvokeMenuConsequence("break_out_menu_accept_on_consequence");
        }, MapEventDisabledMethods);

        var result = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBreakOutResult>());
        Assert.True(result.Approved);
        Assert.Empty(result.CharacterIds);
        AssertRegularTroops(otherMobilePartyId, armyTroops - 30);
        client.Call(() =>
        {
            var behavior = Campaign.Current.GetCampaignBehavior<EncounterGameMenuBehavior>();
            Assert.NotNull(behavior._breakInOutCasualties);
            Assert.Equal(0, behavior._breakInOutCasualties.TotalManCount);
            Assert.Equal(30, behavior._breakInOutArmyCasualties);
        });
    }

    [Fact]
    public void BreakIn_NewAttemptThatStopsEarly_ClearsTheRejectLock()
    {
        var (client, _, _, _) = PrepareBreakInMenu();

        using (new ReliableMessageDeliveryBlocker<NetworkBreakInResult>())
            client.Call(() => InvokeMenuConsequence("break_in_menu_accept_on_consequence"), MapEventDisabledMethods);
        Assert.True(SiegeBreakOut.IsBreakInPending);

        // The encounter moved on, then a new attempt with no besieged settlement stops before sending anything.
        client.Call(() =>
        {
            Campaign.Current.PlayerEncounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
            client.Resolve<IMessageBroker>().Publish(this, new BreakInAttempted(MobileParty.MainParty, null));
            Assert.False(SiegeBreakOut.IsBreakInPending);
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BreakIn_SiegeEndsOrPartyMovesBeforeTheServerReadsTheRequest_IsTurnedDownAndCostsNothing(bool siegeEnds)
    {
        var (client, playerMobilePartyId, siege, troops) = PrepareBreakInMenu();

        // The request is still on its way when the siege ends or the party is moved away.
        using (new ReliableMessageDeliveryBlocker<NetworkRequestBreakIn>())
            client.Call(() => InvokeMenuConsequence("break_in_menu_accept_on_consequence"), MapEventDisabledMethods);
        var request = Assert.Single(client.NetworkSentMessages.GetMessages<NetworkRequestBreakIn>());
        if (siegeEnds)
            EndSiege(siege.SiegeEventId, siege.SettlementId);
        else
            MovePartyAway(playerMobilePartyId);
        Server.NetworkSentMessages.Clear();

        Server.Call(() => Server.SimulateMessage(client.NetPeer, request), MapEventDisabledMethods);
        TestEnvironment.FlushCoalescer();

        Assert.False(Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBreakInResult>()).Approved);
        AssertRegularTroops(playerMobilePartyId, troops);
        AssertPartyOutsideSettlement(Server, playerMobilePartyId);
    }

    [Fact]
    public void BreakIn_SiegeEndsAfterTheServerPaidButBeforeTheReply_ClientKeepsTheServerLossesAndIsTold()
    {
        var (client, playerMobilePartyId, siege, troops) = PrepareBreakInMenu();

        using (new ReliableMessageDeliveryBlocker<NetworkBreakInResult>())
            client.Call(() => InvokeMenuConsequence("break_in_menu_accept_on_consequence"), MapEventDisabledMethods);
        var result = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBreakInResult>());
        Assert.True(result.Approved);
        AssertRegularTroops(playerMobilePartyId, troops - 40);

        EndSiege(siege.SiegeEventId, siege.SettlementId);
        client.InternalMessages.Clear();
        client.NetworkSentMessages.Clear();

        client.SimulateMessage(Server.NetPeer, result);

        AssertRegularTroops(playerMobilePartyId, troops - 40);
        var message = Assert.Single(client.InternalMessages.GetMessages<SendInformationMessage>());
        Assert.Equal("The siege changed before your break in went through. 40 troops were already lost getting there.", message.Text);
        client.Call(() =>
        {
            Assert.NotEqual("break_in_debrief_menu", Campaign.Current.CurrentMenuContext?.GameMenu?.StringId);
            Assert.False(SiegeBreakOut.IsBreakInPending);
            Assert.Null(MobileParty.MainParty.CurrentSettlement);
        });
        Assert.Empty(client.NetworkSentMessages.GetMessages<NetworkRequestBreakInContinuation>());
        AssertPartyOutsideSettlement(Server, playerMobilePartyId);
    }

    [Fact]
    public void BreakIn_PaidReplyArrivesAfterANewerAttempt_TellsThePlayerOnceWithoutEntering()
    {
        var (client, playerMobilePartyId, _, troops) = PrepareBreakInMenu();

        using (new ReliableMessageDeliveryBlocker<NetworkBreakInResult>())
            client.Call(() => InvokeMenuConsequence("break_in_menu_accept_on_consequence"), MapEventDisabledMethods);
        var result = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBreakInResult>());
        Assert.True(result.Approved);
        AssertRegularTroops(playerMobilePartyId, troops - 40);

        // The encounter moved on and a newer attempt replaced the old request before its reply came.
        client.Call(() =>
        {
            Campaign.Current.PlayerEncounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
            client.Resolve<IMessageBroker>().Publish(this, new BreakInAttempted(MobileParty.MainParty, null));
            Assert.False(SiegeBreakOut.IsBreakInPending);
        });
        client.InternalMessages.Clear();
        client.NetworkSentMessages.Clear();

        client.SimulateMessage(Server.NetPeer, result);
        client.SimulateMessage(Server.NetPeer, result);

        AssertRegularTroops(playerMobilePartyId, troops - 40);
        var message = Assert.Single(client.InternalMessages.GetMessages<SendInformationMessage>());
        Assert.Equal("The siege changed before your break in went through. 40 troops were already lost getting there.", message.Text);
        client.Call(() =>
        {
            Assert.NotEqual("break_in_debrief_menu", Campaign.Current.CurrentMenuContext?.GameMenu?.StringId);
            Assert.False(SiegeBreakOut.IsBreakInPending);
            Assert.Null(MobileParty.MainParty.CurrentSettlement);
        });
        Assert.Empty(client.NetworkSentMessages.GetMessages<NetworkRequestBreakInContinuation>());
        AssertPartyOutsideSettlement(Server, playerMobilePartyId);
    }

    [Fact]
    public void BreakIn_OlderPaidReplyWhileANewerAttemptForTheSameSiegeIsPending_ShowsOnlyTheDebrief()
    {
        var (client, playerMobilePartyId, _, troops) = PrepareBreakInMenu();

        var results = AcceptTwiceForTheSameSiege(client);

        Assert.Equal(new[] { true, true }, results.Select(result => result.Approved));
        AssertRegularTroops(playerMobilePartyId, troops - 40);

        // The first reply comes while the newer attempt waits on the same paid receipt.
        client.SimulateMessage(Server.NetPeer, results[0]);
        Assert.Empty(client.InternalMessages.GetMessages<SendInformationMessage>());
        client.Call(() =>
        {
            Assert.Equal("break_in_menu", Campaign.Current.CurrentMenuContext?.GameMenu?.StringId);
            Assert.True(SiegeBreakOut.IsBreakInPending);
        });

        client.SimulateMessage(Server.NetPeer, results[1]);

        Assert.Empty(client.InternalMessages.GetMessages<SendInformationMessage>());
        AssertRegularTroops(playerMobilePartyId, troops - 40);
        client.Call(() =>
        {
            Assert.Equal("break_in_debrief_menu", Campaign.Current.CurrentMenuContext?.GameMenu?.StringId);
            Assert.Equal(40, Campaign.Current.GetCampaignBehavior<EncounterGameMenuBehavior>()._breakInOutCasualties.TotalManCount);
            Assert.False(SiegeBreakOut.IsBreakInPending);
        });
    }

    [Fact]
    public void BreakIn_OlderPaidReplyThenTheNewerAttemptIsTurnedDown_TellsThePlayerOnce()
    {
        var (client, playerMobilePartyId, _, troops) = PrepareBreakInMenu();

        // The party is moved away after the first attempt paid, so the server turns the newer one down.
        var results = AcceptTwiceForTheSameSiege(client, () => MovePartyAway(playerMobilePartyId));

        Assert.Equal(new[] { true, false }, results.Select(result => result.Approved));
        AssertRegularTroops(playerMobilePartyId, troops - 40);

        client.SimulateMessage(Server.NetPeer, results[0]);
        Assert.Empty(client.InternalMessages.GetMessages<SendInformationMessage>());

        client.SimulateMessage(Server.NetPeer, results[1]);

        var message = Assert.Single(client.InternalMessages.GetMessages<SendInformationMessage>());
        Assert.Equal("The siege changed before your break in went through. 40 troops were already lost getting there.", message.Text);
        client.Call(() =>
        {
            Assert.NotEqual("break_in_debrief_menu", Campaign.Current.CurrentMenuContext?.GameMenu?.StringId);
            Assert.False(SiegeBreakOut.IsBreakInPending);
        });
        Assert.Empty(client.NetworkSentMessages.GetMessages<NetworkRequestBreakInContinuation>());
    }

    // Accepts, then accepts again in a new encounter at the same siege before the first reply came. Returns both
    // server replies, held back from the client.
    private NetworkBreakInResult[] AcceptTwiceForTheSameSiege(EnvironmentInstance client, Action? betweenAttempts = null)
    {
        using (new ReliableMessageDeliveryBlocker<NetworkBreakInResult>())
        {
            client.Call(() => InvokeMenuConsequence("break_in_menu_accept_on_consequence"), MapEventDisabledMethods);
            betweenAttempts?.Invoke();
            client.Call(() =>
            {
                var again = ObjectHelper.SkipConstructor<PlayerEncounter>();
                again.EncounterSettlementAux = PlayerEncounter.EncounterSettlement;
                Campaign.Current.PlayerEncounter = again;
                InvokeMenuConsequence("break_in_menu_accept_on_consequence");
            }, MapEventDisabledMethods);
        }
        Assert.Equal(2, client.NetworkSentMessages.GetMessages<NetworkRequestBreakIn>().Select(request => request.RequestId).Distinct().Count());
        client.InternalMessages.Clear();
        client.NetworkSentMessages.Clear();
        return Server.NetworkSentMessages.GetMessages<NetworkBreakInResult>().ToArray();
    }

    // A player outside a besieged town with 200 regulars, looking at a real break in menu that costs 40 troops.
    private (EnvironmentInstance client, string playerMobilePartyId, (string SiegeEventId, string SettlementId, string LeaderPartyId) siege, int troops) PrepareBreakInMenu()
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
        var troops = AddRegularTroops(playerMobilePartyId, 200);
        Server.Call(() => Campaign.Current.Models.TroopSacrificeModel = new FixedTroopSacrificeModel(40));
        OpenBreakInMenu(client);
        Server.NetworkSentMessages.Clear();
        client.NetworkSentMessages.Clear();
        return (client, playerMobilePartyId, siege, troops);
    }

    // Ends the siege the vanilla way on the server, which carries the end to every client.
    private void EndSiege(string siegeEventId, string settlementId)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<SiegeEvent>(siegeEventId, out var siegeEvent));
            siegeEvent.FinalizeSiegeEvent();
        }, MapEventDisabledMethods);
        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
                Assert.Null(settlement.SiegeEvent);
            });
        }
    }

    private void MovePartyAway(string mobilePartyId)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var party));
            using (new AllowedThread())
                party.Position = new CampaignVec2(new Vec2(900f, 900f), isOnLand: true);
        });
    }

    // Call before the request. The player leads an army whose only other party holds every regular, so the leader's own party loses nobody.
    private void LeadArmyWithTroopsInAnotherParty(string leaderMobilePartyId, string memberMobilePartyId)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(leaderMobilePartyId, out var leader));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(memberMobilePartyId, out var member));
            var army = new Army(GameObjectCreator.CreateInitializedObject<Kingdom>(), leader, Army.ArmyTypes.Patrolling);
            member.Army = army;
            foreach (var element in leader.MemberRoster.GetTroopRoster().Where(element => element.Character.IsRegular).ToList())
                leader.MemberRoster.AddToCounts(element.Character, -element.Number);
            Assert.Same(army, leader.Army);
            Assert.Contains(member, army.Parties);
            Assert.Equal(0, leader.MemberRoster.TotalRegulars);
        }, MapEventDisabledMethods);
        TestEnvironment.FlushCoalescer();
        AssertRegularTroops(leaderMobilePartyId, 0);
    }

    // The client always asks for the port, so only the server's own port, ship and fleet state decides.
    private (EnvironmentInstance client, string playerMobilePartyId, int troops) RequestPortBreakOut(
        bool hasPort, bool hasShips, bool anchoredHere)
    {
        var (client, _, playerPartyId, _) = CreateTwoPlayerParties();
        var playerMobilePartyId = GetMobilePartyId(Server, playerPartyId);
        var siege = CreateSyncedSiege();
        TestEnvironment.ConnectRegisteredPlayer(client, "PlayerOne");
        PrepareClientSiegeEncounter(client, playerPartyId, siege.SettlementId);
        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(playerMobilePartyId, out var party));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(siege.SettlementId, out var settlement));
                using (new AllowedThread())
                    party._currentSettlement = settlement;
            });
        }
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(playerMobilePartyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(siege.SettlementId, out var settlement));
            UseNavalMap();
            settlement.PortPosition = new CampaignVec2(new Vec2(10f, 10f), isOnLand: false);
            settlement.HasPort = hasPort;
            party.Party._ships.Clear();
            if (hasShips) party.Party._ships.Add(ObjectHelper.SkipConstructor<Ship>());
            var anchorPosition = new CampaignVec2(new Vec2(anchoredHere ? 10f : 60f, 10f), isOnLand: false)
            {
                _faceCache = new PathFaceRecord(1, 0, 0),
                _isPositionCacheValid = true,
            };
            party.Anchor = new AnchorPoint(party) { Position = anchorPosition };
            Campaign.Current.Models.TroopSacrificeModel = new FixedTroopSacrificeModel(30);
        });
        var troops = AddRegularTroops(playerMobilePartyId, 200);
        Server.NetworkSentMessages.Clear();

        client.Call(() =>
        {
            Campaign.Current.GetCampaignBehavior<EncounterGameMenuBehavior>()._isBreakingOutFromPort = true;
            InvokeMenuConsequence("break_out_menu_accept_on_consequence");
        }, MapEventDisabledMethods);
        return (client, playerMobilePartyId, troops);
    }

    // Call inside Server.Call. Gives the headless campaign a sea face and the town encounter distance AnchorPoint.IsAtSettlement reads.
    private void UseNavalMap()
    {
        var map = new Mock<IMapScene>();
        map.Setup(scene => scene.GetFaceTerrainType(It.IsAny<PathFaceRecord>())).Returns(TerrainType.Water);
        Campaign.Current._mapSceneWrapper = map.Object;
        var models = Campaign.Current.Models.GetGameModels().ToList();
        models.Add(new DefaultEncounterModel());
        models.Add(new NavalPartyNavigationModel());
        var gameModels = new GameModels(models);
        Server.GameInstance.Game._gameModelManagers[typeof(GameModels)] = gameModels;
        Campaign.Current._gameModels = gameModels;
    }

    private sealed class NavalPartyNavigationModel : DefaultPartyNavigationModel
    {
        public override bool IsTerrainTypeValidForNavigationType(TerrainType terrainType, MobileParty.NavigationType navigationType) =>
            navigationType == MobileParty.NavigationType.Naval
                ? terrainType == TerrainType.Water
                : base.IsTerrainTypeValidForNavigationType(terrainType, navigationType);
    }

    private sealed class FixedTroopSacrificeModel : DefaultTroopSacrificeModel
    {
        private readonly int losses;
        private readonly int gateLosses;

        public FixedTroopSacrificeModel(int losses, int gateLosses = 0)
        {
            this.losses = losses;
            this.gateLosses = gateLosses;
        }

        public override ExplainedNumber GetLostTroopCountForBreakingInBesiegedSettlement(MobileParty party, SiegeEvent siegeEvent) =>
            new ExplainedNumber(losses);

        public override ExplainedNumber GetLostTroopCountForBreakingOutOfBesiegedSettlement(MobileParty party, SiegeEvent siegeEvent, bool isBreakingOutFromPort) =>
            new ExplainedNumber(isBreakingOutFromPort ? losses : gateLosses);
    }

    private static void InvokeMenuConsequence(string methodName)
    {
        var method = AccessTools.Method(typeof(EncounterGameMenuBehavior), methodName);
        Assert.NotNull(method);
        method.Invoke(Campaign.Current.GetCampaignBehavior<EncounterGameMenuBehavior>(), new object?[] { null });
    }

    // Opens a real break in menu, so leaving it through Reject changes the menu the pending request belongs to.
    private static void OpenBreakInMenu(EnvironmentInstance client)
    {
        client.Call(() =>
        {
            var starter = new CampaignGameStarter(Campaign.Current.GameMenuManager, Campaign.Current.ConversationManager);
            foreach (var menuId in new[] { "break_in_menu", "break_in_debrief_menu", "join_siege_event" })
                if (Campaign.Current.GameMenuManager.GetGameMenu(menuId) == null)
                    starter.AddGameMenu(menuId, "{=!}" + menuId, null);
            var context = Game.Current.ObjectManager.CreateObject<MenuContext>();
            var mapState = Game.Current.GameStateManager.CreateState<MapState>();
            mapState._menuContext = context;
            Game.Current.GameStateManager._gameStates.Add(mapState);
            context.SwitchToMenu("break_in_menu");
            Assert.Equal("break_in_menu", Campaign.Current.CurrentMenuContext?.GameMenu?.StringId);
        });
    }

    // Call inside Server.Call. Pays the break in troops, which the server needs before the party may enter.
    private void PayBreakIn(EnvironmentInstance client, MobileParty party, Settlement settlement)
    {
        Assert.True(Server.ObjectManager.TryGetHandle(party, out var partyId));
        Assert.True(Server.ObjectManager.TryGetHandle(settlement, out var settlementId));
        Assert.True(Server.ObjectManager.TryGetHandle(settlement.SiegeEvent, out var siegeId));
        Server.SimulateMessage(client.NetPeer, new NetworkRequestBreakIn("paid-break-in", partyId, settlementId, siegeId));
    }

    private int AddRegularTroops(string mobilePartyId, int count)
    {
        var troopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        var total = 0;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
            party.MemberRoster.AddToCounts(troop, count);
            total = party.MemberRoster.TotalRegulars;
        });
        TestEnvironment.FlushCoalescer();
        AssertRegularTroops(mobilePartyId, total);
        return total;
    }

    private void AssertRegularTroops(string mobilePartyId, int expected)
    {
        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var party));
                Assert.Equal(expected, party.MemberRoster.TotalRegulars);
            });
        }
    }
}
