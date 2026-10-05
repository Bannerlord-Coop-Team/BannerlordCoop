using Common;
using Common.Network;
using Common.Util;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Client.Services.SiegeEvents.Handlers;
using Coop.Core.Client.Services.SiegeEvents.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using Coop.Core.Server.Services.SiegeEvents.Messages;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.MapEvents;
using E2E.Tests.Util;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.MapEventSides.Messages;
using GameInterface.Services.MapEvents.Extensions;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.Settlements.Interfaces;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.SiegeEvents.Interfaces;
using GameInterface.Services.Villages.Interfaces;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.SiegeEvents;

public class SiegeEntryValidationFlowTests : MapEventTestBase
{
    private static IReadOnlyList<MethodBase> SiegeCreationDisabledMethods => new[]
    {
        AccessTools.Method(
            typeof(MobileParty),
            nameof(MobileParty.OnPartyJoinedSiegeInternal)),
        AccessTools.Method(
            typeof(BesiegerCamp),
            nameof(BesiegerCamp.InitializeSiegeEventSide)),
        AccessTools.Method(
            typeof(Settlement),
            nameof(Settlement.InitializeSiegeEventSide)),
        AccessTools.Method(
            typeof(ChangeRelationAction),
            "ApplyInternal"),
    };

    public SiegeEntryValidationFlowTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void SettlementEncounterRequest_WhenPartyIsFarFromSettlement_IsRejected()
    {
        var client = Clients.First();
        var context = CreateEntryContext(client);
        var farPosition = Position(900f, 900f);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            using (new AllowedThread())
            {
                party.Position = farPosition;
            }
        });

        client.Call(() => client.Resolve<INetwork>().SendAll(
            new NetworkRequestStartSettlementEncounter(
                client.GetHandle<MobileParty>(context.PartyId),
                client.GetHandle<Settlement>(context.SettlementId))));

        Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkSettlementEncounterRejected>());
        AssertInformationMessage(
            client,
            "Unable to enter the settlement: your party is too far from the settlement.");
        Assert.Empty(
            Server.NetworkSentMessages.GetMessages<NetworkStartSettlementEncounter>());
        Assert.Empty(
            Server.NetworkSentMessages.GetMessages<NetworkPartyEnterSettlement>());
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            Assert.Null(party.CurrentSettlement);
            Assert.True(party.Position.Distance(farPosition) < 0.001f);
        });

        client.InternalMessages.Clear();
        SendBesiegeRequest(client, context);

        Assert.False(GetBesiegeApproval().Approved);
        AssertInformationMessage(
            client,
            "Unable to begin the siege: your party is too far from the settlement.");
        AssertNoSiege(context);
    }

    [Fact]
    public void SettlementEncounterRequest_ForPartyControlledByAnotherPeer_IsRejected()
    {
        var owner = Clients.First();
        var requester = Clients.Skip(1).First();
        var context = CreateEntryContext(owner);
        ConnectAdditionalPlayer(requester, "PlayerTwo");

        requester.Call(() => requester.Resolve<INetwork>().SendAll(
            new NetworkRequestStartSettlementEncounter(
                requester.GetHandle<MobileParty>(context.PartyId),
                requester.GetHandle<Settlement>(context.SettlementId))));

        Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkSettlementEncounterRejected>());
        AssertInformationMessage(
            requester,
            "Unable to enter the settlement: your party is not controlled by you.");
        Assert.Empty(
            Server.NetworkSentMessages.GetMessages<NetworkStartSettlementEncounter>());
        Assert.Empty(
            Server.NetworkSentMessages.GetMessages<NetworkPartyEnterSettlement>());
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            Assert.Null(party.CurrentSettlement);
        });
    }

    [Fact]
    public void EndSettlementEncounterRequest_ForPartyControlledByAnotherPeer_IsRejected()
    {
        var owner = Clients.First();
        var requester = Clients.Skip(1).First();
        var context = CreateEntryContext(owner);
        ConnectAdditionalPlayer(requester, "PlayerTwo");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(
                context.SettlementId,
                out var settlement));
            using (new AllowedThread())
            {
                party.CurrentSettlement = settlement;
            }
        });
        ClearMessages();

        requester.Call(() => requester.Resolve<INetwork>().SendAll(
            new NetworkRequestEndSettlementEncounter(
                requester.GetHandle<MobileParty>(context.PartyId))));

        var result = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkSettlementEncounterLeaveResult>());
        Assert.Equal(SettlementEncounterLeaveOutcome.Suppressed, result.Outcome);
        AssertInformationMessage(
            requester,
            "Unable to leave the settlement: your party is not controlled by you.");
        Assert.Empty(
            Server.NetworkSentMessages.GetMessages<NetworkPartyLeaveSettlement>());
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(
                context.SettlementId,
                out var settlement));
            Assert.Same(settlement, party.CurrentSettlement);
        });
    }

    [Fact]
    public void BesiegeRequest_WhenPartyIsFarFromSettlement_IsRejected()
    {
        var client = Clients.First();
        var context = CreateEntryContext(client);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            using (new AllowedThread())
            {
                party.Position = Position(900f, 900f);
            }
        });

        SendBesiegeRequest(client, context);

        Assert.False(GetBesiegeApproval().Approved);
        AssertInformationMessage(
            client,
            "Unable to begin the siege: your party is too far from the settlement.");
        AssertNoSiege(context);
    }

    [Fact]
    public void BesiegeRequest_ForOwnSettlement_IsRejected()
    {
        var client = Clients.First();
        var context = CreateEntryContext(client);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Town>(
                context.TownId,
                out var town));
            using (new AllowedThread())
            {
                town.OwnerClan = party.ActualClan;
            }
        });

        SendBesiegeRequest(client, context);

        Assert.False(GetBesiegeApproval().Approved);
        AssertInformationMessage(
            client,
            "Unable to begin the siege: your party belongs to the defending faction.");
        AssertNoSiege(context);
    }

    [Fact]
    public void BesiegeRequest_WithValidServerState_IsApproved()
    {
        var client = Clients.First();
        var context = CreateEntryContext(client);
        IgnoreEntryResults(client);

        SendBesiegeRequest(client, context, SiegeCreationDisabledMethods);

        Assert.True(GetBesiegeApproval().Approved);
        AssertSiegeStarted(context);
    }

    [Fact]
    public void BesiegeRequest_WhenPartyAlreadyBesiegesTarget_IsApproved()
    {
        var client = Clients.First();
        var context = CreateEntryContext(client);
        IgnoreEntryResults(client);

        SendBesiegeRequest(client, context, SiegeCreationDisabledMethods);

        SiegeEvent? originalSiege = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(
                context.SettlementId,
                out var settlement));
            originalSiege = settlement.SiegeEvent;
            Assert.NotNull(originalSiege);
        });
        ClearMessages();

        SendBesiegeRequest(client, context);

        Assert.True(GetBesiegeApproval().Approved);
        Assert.Empty(client.InternalMessages.GetMessages<SendInformationMessage>());
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(
                context.SettlementId,
                out var settlement));
            var currentSiege = settlement.SiegeEvent;
            Assert.NotNull(currentSiege);
            Assert.Same(originalSiege, currentSiege);
            Assert.Same(currentSiege.BesiegerCamp, party.BesiegerCamp);
        });
    }

    [Fact]
    public void JoinRequest_WithValidServerState_IsApproved()
    {
        var client = Clients.First();
        var context = CreateEntryContext(client);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(
                context.SettlementId,
                out var settlement));

            using (new AllowedThread())
            {
                CreatePresentedSiege(party, settlement);
            }

            Assert.True(settlement.SiegeEvent.CanPartyJoinSide(
                party.Party,
                BattleSideEnum.Attacker));
        }, SiegeCreationDisabledMethods);
        Server.NetworkSentMessages.Clear();
        IgnoreEntryResults(client);

        SendJoinRequest(client, context, SiegeCreationDisabledMethods);

        var approval = GetJoinApproval();
        Assert.True(approval.Approved);
        Assert.Equal(context.SettlementId, approval.SettlementId);
        AssertJoinedSiege(context);
    }

    [Fact]
    public void JoinRequest_WhenPartyIsFarFromSettlement_IsRejected()
    {
        var client = Clients.First();
        var context = CreateJoinContext(client);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            using (new AllowedThread())
            {
                party.Position = Position(900f, 900f);
            }
        });

        SendJoinRequest(client, context);

        Assert.False(GetJoinApproval().Approved);
        AssertInformationMessage(
            client,
            "Unable to join the siege: your party is too far from the settlement.");
        AssertNotJoined(context);
    }

    [Fact]
    public void JoinRequest_WhenPartyBelongsToDefender_IsRejected()
    {
        var client = Clients.First();
        var context = CreateJoinContext(client);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Town>(
                context.TownId,
                out var town));
            using (new AllowedThread())
            {
                town.OwnerClan = party.ActualClan;
            }
        });

        SendJoinRequest(client, context);

        Assert.False(GetJoinApproval().Approved);
        AssertInformationMessage(
            client,
            "Unable to join the siege: your party belongs to the defending faction.");
        AssertNotJoined(context);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GenericBattleJoinMenu_RequestsMissingEncounterOnceAndPreservesExistingEncounter(bool hasEncounter)
    {
        var client = Clients.First();
        var context = CreateEntryContext(client);
        var battle = CreateServerMapEvent();
        client.NetworkSentMessages.Clear();

        client.Call(() =>
        {
            var settlement = PrepareClientMenuContext(client, context);
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(battle.MapEventId, out var mapEvent));
            using (new AllowedThread())
            {
                MobileParty.MainParty.CurrentSettlement = settlement;
                settlement.Party._mapEventSide = mapEvent.DefenderSide;
            }
            var encounter = hasEncounter ? ObjectHelper.SkipConstructor<PlayerEncounter>() : null;
            Campaign.Current.PlayerEncounter = encounter;
            var model = new DefaultEncounterGameMenuModel();

            Assert.Equal(hasEncounter ? "join_encounter" : null, model.GetGenericStateMenu());
            Assert.Equal(hasEncounter ? "join_encounter" : null, model.GetGenericStateMenu());
            Assert.Same(encounter, PlayerEncounter.Current);
            Assert.Same(settlement, MobileParty.MainParty.CurrentSettlement);
            Assert.Null(MobileParty.MainParty.MapEvent);
        }, WithoutNetworkDelivery());

        var requests = client.NetworkSentMessages.GetMessages<NetworkRequestStartSettlementEncounter>();
        if (hasEncounter)
        {
            Assert.Empty(requests);
        }
        else
        {
            var request = Assert.Single(requests);
            Assert.Equal(client.GetHandle<MobileParty>(context.PartyId), request.PartyId);
            Assert.Equal(client.GetHandle<Settlement>(context.SettlementId), request.SettlementId);
        }
    }

    [Fact]
    public void GenericTownMenu_WithoutBattle_DoesNotRequestEncounterRecovery()
    {
        var client = Clients.First();
        var context = CreateEntryContext(client);
        client.Call(() =>
        {
            var settlement = PrepareClientMenuContext(client, context);
            using (new AllowedThread()) MobileParty.MainParty.CurrentSettlement = settlement;
            Campaign.Current.PlayerEncounter = null;

            Assert.Equal("town_outside", new DefaultEncounterGameMenuModel().GetGenericStateMenu());
        }, WithoutNetworkDelivery());

        Assert.Empty(client.NetworkSentMessages.GetMessages<NetworkRequestStartSettlementEncounter>());
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void RejectedGenericBattleRecovery_RetriesAfterExplicitEntryOrBattleChange(bool explicitEntry, bool differentBattle, bool leaveBattle)
    {
        var client = Clients.First();
        var context = CreateEntryContext(client);
        var battle = CreateServerMapEvent();
        var nextBattle = differentBattle ? CreateServerMapEvent() : battle;
        client.NetworkSentMessages.Clear();

        client.Call(() =>
        {
            var settlement = PrepareClientMenuContext(client, context);
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(battle.MapEventId, out var mapEvent));
            using (new AllowedThread())
            {
                MobileParty.MainParty.CurrentSettlement = settlement;
                settlement.Party._mapEventSide = mapEvent.DefenderSide;
            }
            Campaign.Current.PlayerEncounter = null;
            Assert.Null(new DefaultEncounterGameMenuModel().GetGenericStateMenu());
        }, WithoutNetworkDelivery());

        var request = Assert.Single(client.NetworkSentMessages.GetMessages<NetworkRequestStartSettlementEncounter>());
        client.SimulateMessage(Server.NetPeer, new NetworkSettlementEncounterRejected(request));

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(context.SettlementId, out var settlement));
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(nextBattle.MapEventId, out var mapEvent));
            using (new AllowedThread()) settlement.Party._mapEventSide = mapEvent.DefenderSide;

            if (leaveBattle)
            {
                using (new AllowedThread()) MobileParty.MainParty.Party.MapEventSide = mapEvent.DefenderSide;
                Assert.True(client.ObjectManager.TryGetId(MobileParty.MainParty.Party, out var partyBaseId));
                client.SimulateMessage(Server.NetPeer, new NetworkPartyLeftBattle(partyBaseId, finishLocalMenus: false));
                Assert.Null(MobileParty.MainParty.MapEvent);
            }
            if (explicitEntry)
                client.SimulateMessage(this, new StartSettlementEncounterAttempted(MobileParty.MainParty, settlement));
            Assert.Null(new DefaultEncounterGameMenuModel().GetGenericStateMenu());
            Assert.Null(PlayerEncounter.Current);
        }, WithoutNetworkDelivery());

        Assert.Equal(explicitEntry || differentBattle || leaveBattle ? 2 : 1,
            client.NetworkSentMessages.GetMessages<NetworkRequestStartSettlementEncounter>().Count());
    }

    [Fact]
    public void RecoveredCityAssault_ExplicitJoinLeavesTownOnAllPeersAndPreservesEncounter()
    {
        var client = Clients.First();
        var context = CreateEntryContext(client);
        var battle = CreateServerMapEvent();
        using var activation = new MethodCallRecorder(Priority.Last,
            AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ActivateGameMenu), new[] { typeof(string) }));
        using var menuSwitch = new MethodCallRecorder(
            AccessTools.Method(typeof(MenuContext), nameof(MenuContext.SwitchToMenu), new[] { typeof(string) }));
        var disabledMethods = MapEventDisabledMethods.Concat(SiegeCreationDisabledMethods).ToList();
        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(context.PartyId, out var party));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(context.SettlementId, out var settlement));
                Assert.True(instance.ObjectManager.TryGetObject<Town>(context.TownId, out var town));
                Assert.True(instance.ObjectManager.TryGetObject<Clan>(context.DefenderClanId, out var defenderClan));
                Assert.True(instance.ObjectManager.TryGetObject<MapEvent>(battle.MapEventId, out var mapEvent));
                using (new AllowedThread())
                {
                    settlement.SetSettlementComponent(town);
                    town.OwnerClan = defenderClan;
                    party.CurrentSettlement = settlement;
                    settlement.Party._mapEventSide = mapEvent.DefenderSide;
                    mapEvent.DefenderSide.LeaderParty = settlement.Party;
                    mapEvent.SetBattleType(MapEvent.BattleTypes.Siege);
                    mapEvent.SetMapEventSettlement(settlement);
                    CreatePresentedSiege(party, settlement);
                    // The skipped scene initializer also creates the collections read by siege strength calculations.
                    settlement.SiegeEvent.BesiegerCamp.SiegeEngines = new SiegeEvent.SiegeEnginesContainer(
                        BattleSideEnum.Attacker, null);
                    settlement.SiegeEngines = new SiegeEvent.SiegeEnginesContainer(
                        BattleSideEnum.Defender, null);
                }
            }, disabledMethods);
        }
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(context.SettlementId, out var settlement));
            Assert.True(Campaign.Current.Models.CombatSimulationModel.GetSettlementAdvantage(settlement) > 0f);
        });
        ClearMessages();
        PlayerEncounter? recoveredEncounter = null;

        client.Call(() =>
        {
            var settlement = PrepareClientMenuContext(client, context);
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(battle.MapEventId, out var mapEvent));
            Campaign.Current.PlayerEncounter = null;
            Assert.True(mapEvent.CanPartyJoinBattle(PartyBase.MainParty, settlement.BattleSide));
            using (new AllowedThread())
                client.Resolve<ISettlementInterface>().StartSettlementEncounter(MobileParty.MainParty, settlement);

            Assert.NotNull(PlayerEncounter.Current);
            Assert.Same(settlement, PlayerEncounter.EncounterSettlement);
            Assert.Same(mapEvent, PlayerEncounter.EncounteredBattle);
            Assert.Null(PlayerEncounter.Battle);
            Assert.Null(MobileParty.MainParty.MapEvent);
            Assert.Equal(new[] { "join_encounter" }, activation.MenusFor(client));
            Assert.Empty(client.NetworkSentMessages.GetMessages<NetworkRequestCreateMapEvent>());
            Assert.Empty(client.NetworkSentMessages.GetMessages<NetworkRequestJoinBattle>());

            recoveredEncounter = PlayerEncounter.Current;
            Assert.Equal("join_encounter", new DefaultEncounterGameMenuModel().GetGenericStateMenu());
            Assert.Same(recoveredEncounter, PlayerEncounter.Current);
            Assert.Empty(client.NetworkSentMessages.GetMessages<NetworkRequestStartSettlementEncounter>());

            // Activation is recorded without rendering, so supply the menu context it would create.
            var mapState = Game.Current.GameStateManager.CreateState<MapState>();
            mapState._menuContext = ObjectHelper.SkipConstructor<MenuContext>();
            mapState._menuContext.GameMenu = new GameMenu("join_encounter");
            Game.Current.GameStateManager._gameStates.Add(mapState);

            new EncounterGameMenuBehavior().game_menu_join_encounter_help_attackers_on_consequence(
                new MenuCallbackArgs((MenuContext)null, null));
            Assert.Null(MobileParty.MainParty.MapEvent);
        }, disabledMethods);

        var request = Assert.Single(client.NetworkSentMessages.GetMessages<NetworkRequestJoinBattle>());
        Assert.Equal(battle.MapEventId, request.MapEventId);
        Assert.Equal(BattleSideEnum.Attacker, request.Side);
        Assert.Empty(client.NetworkSentMessages.GetMessages<NetworkRequestCreateMapEvent>());
        Assert.True(Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkJoinBattleReply>()).Accepted);
        var sent = Server.NetworkSentMessages.ToList();
        var leave = Assert.Single(sent.OfType<NetworkPartyLeaveSettlement>());
        Assert.True(sent.IndexOf(leave) < sent.FindIndex(message => message is NetworkAddBattleParty));
        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(context.PartyId, out var party));
                Assert.True(instance.ObjectManager.TryGetObject<MapEvent>(battle.MapEventId, out var mapEvent));
                Assert.Null(party.CurrentSettlement);
                Assert.Same(mapEvent, party.MapEvent);
                Assert.Same(mapEvent.AttackerSide, party.Party.MapEventSide);
                Assert.NotNull(mapEvent.FindMapEventParty(party.Party));
            });
        }
        client.Call(() =>
        {
            Assert.Same(recoveredEncounter, PlayerEncounter.Current);
            Assert.Same(MobileParty.MainParty.MapEvent, PlayerEncounter.Battle);
            Assert.True(PlayerEncounter.Current.IsJoinedBattle);
            Assert.Contains("encounter", menuSwitch.MenusFor(client));
            var attack = new MenuCallbackArgs((MenuContext)null, null);
            Assert.True(new EncounterGameMenuBehavior().game_menu_encounter_attack_on_condition(attack));
            Assert.True(attack.IsEnabled);
        });
    }

    [Theory]
    [InlineData(MapEvent.BattleTypes.Siege, true, "encounter")]
    [InlineData(MapEvent.BattleTypes.FieldBattle, false, "encounter")]
    public void SettlementEncounterMenu_PreservesDefenderAndNonAssaultMenus(
        MapEvent.BattleTypes battleType, bool sameFaction, string expectedMenu)
    {
        var client = Clients.First();
        var context = CreateEntryContext(client);
        var battle = CreateServerMapEvent();
        client.Call(() =>
        {
            var settlement = PrepareClientMenuContext(client, context);
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(battle.MapEventId, out var mapEvent));
            using (new AllowedThread())
            {
                MobileParty.MainParty.CurrentSettlement = settlement;
                settlement.Party._mapEventSide = mapEvent.DefenderSide;
                mapEvent.SetBattleType(battleType);
                if (sameFaction) settlement.Town.OwnerClan = MobileParty.MainParty.ActualClan;
            }
            Assert.Equal(expectedMenu, new DefaultEncounterGameMenuModel().GetEncounterMenu(
                MobileParty.MainParty.Party, settlement.Party, out var startBattle, out var joinBattle));
            Assert.False(startBattle);
            Assert.False(joinBattle);
        }, WithoutNetworkDelivery());
    }

    private static Settlement PrepareClientMenuContext(EnvironmentInstance client, EntryContext context)
    {
        Assert.True(client.ObjectManager.TryGetObject<MobileParty>(context.PartyId, out var party));
        Assert.True(client.ObjectManager.TryGetObject<Settlement>(context.SettlementId, out var settlement));
        Assert.True(client.ObjectManager.TryGetObject<Town>(context.TownId, out var town));
        Assert.True(client.ObjectManager.TryGetObject<Clan>(context.DefenderClanId, out var defenderClan));
        using (new AllowedThread())
        {
            // Player registration does not replace the client's bootstrap main party.
            Campaign.Current.MainParty = party;
            // CreateEntryContext wires the server city under AllowedThread, so prepare its client copy too.
            settlement.SetSettlementComponent(town);
            town.OwnerClan = defenderClan;
        }

        Assert.Same(party, MobileParty.MainParty);
        Assert.True(client.ObjectManager.TryGetHandle(MobileParty.MainParty, out _));
        Assert.True(settlement.IsTown);
        Assert.Equal(KillCharacterAction.KillCharacterActionDetail.None, Hero.MainHero.DeathMark);
        Assert.Null(MobileParty.MainParty.MapEvent);
        Assert.Null(MobileParty.MainParty.BesiegerCamp);
        Assert.Null(MobileParty.MainParty.AttachedTo);
        Assert.NotEqual(MobileParty.MainParty.MapFaction, settlement.MapFaction);
        return settlement;
    }

    private EntryContext CreateEntryContext(EnvironmentInstance client)
    {
        const string controllerId = "PlayerOne";
        var (_, partyId) = CreatePlayerHeroParty(controllerId);
        TestEnvironment.ConnectRegisteredPlayer(client, controllerId);
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var townId = TestEnvironment.CreateRegisteredObject<Town>();
        var defenderClanId = TestEnvironment.CreateRegisteredObject<Clan>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                partyId,
                out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(
                settlementId,
                out var settlement));
            Assert.True(Server.ObjectManager.TryGetObject<Town>(
                townId,
                out var town));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(
                defenderClanId,
                out var defenderClan));

            using (new AllowedThread())
            {
                settlement.Town = town;
                settlement.SetSettlementComponent(town);
                settlement.Party = new PartyBase(settlement);
                settlement.GatePosition = Position(20f, 30f);
                town.OwnerClan = defenderClan;
                town.IsOwnerUnassigned = false;
                party.ActualClan.Id = new MBGUID(1);
                defenderClan.Id = new MBGUID(2);
                party.Position = settlement.GatePosition;
                party.IsActive = true;
            }

            if (party.Party.NumberOfHealthyMembers == 0)
                party.MemberRoster.AddToCounts(party.LeaderHero.CharacterObject, 1);

            VillageHostileFactionStanceHelper.ApplyWarStance(
                party.ActualClan,
                defenderClan);
        });

        Server.NetworkSentMessages.Clear();
        client.NetworkSentMessages.Clear();
        foreach (var connectedClient in Clients)
            connectedClient.InternalMessages.Clear();
        return new EntryContext(partyId, settlementId, townId, defenderClanId);
    }

    private EntryContext CreateJoinContext(EnvironmentInstance client)
    {
        var context = CreateEntryContext(client);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(
                context.SettlementId,
                out var settlement));
            using (new AllowedThread())
            {
                CreatePresentedSiege(party, settlement);
            }
        }, SiegeCreationDisabledMethods);
        Server.NetworkSentMessages.Clear();
        return context;
    }

    private void ConnectAdditionalPlayer(EnvironmentInstance client, string controllerId)
    {
        CreatePlayerHeroParty(controllerId);
        TestEnvironment.ConnectRegisteredPlayer(client, controllerId);
        ClearMessages();
    }

    private void ClearMessages()
    {
        Server.NetworkSentMessages.Clear();
        foreach (var client in Clients)
        {
            client.NetworkSentMessages.Clear();
            client.InternalMessages.Clear();
        }
    }

    private void SendBesiegeRequest(
        EnvironmentInstance client,
        EntryContext context,
        IEnumerable<MethodBase>? disabledMethods = null)
    {
        client.Call(
            () => client.Resolve<INetwork>().SendAll(
                new NetworkRequestBesiegeSettlement(
                    context.PartyId,
                    context.SettlementId)),
            disabledMethods);
    }

    private static void SendJoinRequest(
        EnvironmentInstance client,
        EntryContext context,
        IEnumerable<MethodBase>? disabledMethods = null)
    {
        client.Call(
            () => client.Resolve<INetwork>().SendAll(
                new NetworkRequestJoinSiegeCamp(
                    context.PartyId,
                    context.SettlementId)),
            disabledMethods);
    }

    private static void IgnoreEntryResults(EnvironmentInstance client) =>
        client.Resolve<ClientSiegeEntryHandler>().Dispose();

    private NetworkBesiegeSettlementApproved GetBesiegeApproval() =>
        Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkBesiegeSettlementApproved>());

    private NetworkJoinSiegeCampApproved GetJoinApproval() =>
        Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkJoinSiegeCampApproved>());

    private void AssertInformationMessage(
        EnvironmentInstance client,
        string expectedText)
    {
        var message = Assert.Single(
            client.InternalMessages.GetMessages<SendInformationMessage>());
        Assert.Equal(expectedText, message.Text);

        foreach (var otherClient in Clients.Where(otherClient => otherClient != client))
            Assert.Empty(otherClient.InternalMessages.GetMessages<SendInformationMessage>());
    }

    private void AssertNoSiege(EntryContext context)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(
                context.SettlementId,
                out var settlement));
            Assert.Null(settlement.SiegeEvent);
        });
    }

    private void AssertSiegeStarted(EntryContext context)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(
                context.SettlementId,
                out var settlement));
            Assert.NotNull(settlement.SiegeEvent);
            Assert.Same(settlement.SiegeEvent.BesiegerCamp, party.BesiegerCamp);
        });
    }

    private void AssertJoinedSiege(EntryContext context)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(
                context.SettlementId,
                out var settlement));
            Assert.Same(settlement.SiegeEvent.BesiegerCamp, party.BesiegerCamp);
        });
    }

    private void AssertNotJoined(EntryContext context)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                context.PartyId,
                out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(
                context.SettlementId,
                out var settlement));
            Assert.NotNull(settlement.SiegeEvent);
            Assert.Null(party.BesiegerCamp);
        });
    }

    private static void CreatePresentedSiege(
        MobileParty joiningParty,
        Settlement settlement)
    {
        var siegeLeader = GameObjectCreator.CreateInitializedObject<MobileParty>();
        siegeLeader.ActualClan = joiningParty.ActualClan;
        siegeLeader.LeaderHero.Clan = joiningParty.ActualClan;
        var ownerClan = settlement.Town.OwnerClan;
        settlement.Town.OwnerClan = null;
        var siegeEvent = new SiegeEvent(settlement, siegeLeader);
        settlement.Town.OwnerClan = ownerClan;
        siegeEvent.BesiegerCamp._besiegerParties.Add(siegeLeader);
        siegeEvent.BesiegerCamp._leaderParty = siegeLeader;
    }

    private static CampaignVec2 Position(float x, float y) =>
        new CampaignVec2(new Vec2(x, y), isOnLand: true);

    private sealed record EntryContext(
        string PartyId,
        string SettlementId,
        string TownId,
        string DefenderClanId);
}
