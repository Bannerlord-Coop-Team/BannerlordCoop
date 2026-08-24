using Common.Messaging;
using Autofac;
using Common.Network;
using Common.Util;
using Coop.Core.Client.Services.SiegeEvents.Messages;
using Coop.Core.Server.Services.SiegeEvents.Messages;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.MapEvents;
using E2E.Tests.Util;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Extensions;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Logging;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.MapEventSides.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.SiegeEvents;
using GameInterface.Services.SiegeEvents.Interfaces;
using HarmonyLib;
using Helpers;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.SiegeEvents;

public class SiegeAssaultLeaveTests : MapEventTestBase
{
    public SiegeAssaultLeaveTests(ITestOutputHelper output) : base(output)
    {
    }

    private static List<MethodBase> SiegeCreationDisabledMethods => new()
    {
        AccessTools.Method(typeof(MobileParty), nameof(MobileParty.OnPartyJoinedSiegeInternal)),
        AccessTools.Method(typeof(BesiegerCamp), nameof(BesiegerCamp.InitializeSiegeEventSide)),
        AccessTools.Method(typeof(Settlement), nameof(Settlement.InitializeSiegeEventSide)),
        AccessTools.Method(typeof(CultureObject), nameof(CultureObject.HasFeat)),
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActiveSiegeAttacker_WithOrWithoutServerCamp_CanLeave(bool serverHasCamp)
    {
        var mapEvent = CreateServerMapEvent();
        var partyId = JoinNewServerPartyToSide(mapEvent.MapEventId, BattleSideEnum.Attacker);
        SetMapEventType(mapEvent.MapEventId, MapEvent.BattleTypes.Siege);
        var leavingClient = Clients.First();
        SetMainParty(leavingClient, partyId);
        var siegeEventId = SetClientOnlyCamp(leavingClient, partyId);
        if (serverHasCamp)
            SetCamp(Server, partyId, siegeEventId);
        string? partyBaseId = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetId(party.Party, out partyBaseId));
        });

        Assert.NotNull(partyBaseId);
        AssertPartyState(Server, partyId, expectMapEvent: true, expectCamp: serverHasCamp);
        AssertPartyState(leavingClient, partyId, expectMapEvent: true, expectCamp: true);
        Server.NetworkSentMessages.Clear();

        leavingClient.Call(() =>
        {
            leavingClient.Resolve<INetwork>().SendAll(new NetworkRequestBreakSiege(partyId, finishLocalMenus: true));
        }, MapEventDisabledMethods
            .Concat(SiegeCreationDisabledMethods)
            .Append(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ExitToLast)))
            .ToList());

        var left = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkPartyLeftBattle>());
        Assert.Equal(partyBaseId, left.PartyId);
        Assert.True(left.LeaveSiege);

        var approval = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBreakSiegeApproved>());
        Assert.Equal(SiegeBreakOutcome.Applied, approval.Outcome);
        Assert.True(approval.BattleLeaveApplied);

        AssertPartyState(Server, partyId, expectMapEvent: false, expectCamp: false);
        foreach (var client in Clients)
        {
            AssertPartyState(client, partyId, expectMapEvent: false, expectCamp: false);
        }
    }

    [Fact]
    public void ActiveSiegeAttacker_LeaveThenRejoin_PreservesClientTracker()
    {
        var mapEventContext = CreateServerMapEvent();
        var partyId = JoinNewServerPartyToSide(mapEventContext.MapEventId, BattleSideEnum.Attacker);
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        RegisterAsPlayerParty("PlayerOne", heroId, partyId);
        var client = Clients.First();
        TestEnvironment.ConnectRegisteredPlayer(client, "PlayerOne");
        string? partyBaseId = null;
        string? trackerId = null;
        string? mapEventPartyId = null;
        string? attackerSideId = null;
        string[]? involvedPartyIds = null;

        SetMapEventType(mapEventContext.MapEventId, MapEvent.BattleTypes.Siege);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetId(party.Party, out partyBaseId));
        });

        SetMainParty(client, partyId);
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            var mapEventParty = mapEvent.FindMapEventParty(party.Party);
            Assert.NotNull(mapEventParty);
            Assert.NotNull(mapEvent.TroopUpgradeTracker);
            Assert.True(client.ObjectManager.TryGetId(mapEvent.TroopUpgradeTracker, out trackerId));
            Assert.True(client.ObjectManager.TryGetId(mapEventParty, out mapEventPartyId));
            Assert.True(client.ObjectManager.TryGetId(mapEvent.AttackerSide, out attackerSideId));
        });

        Assert.NotNull(partyBaseId);
        Assert.NotNull(trackerId);
        Assert.NotNull(mapEventPartyId);
        Assert.NotNull(attackerSideId);
        client.Call(() => client.Resolve<INetwork>().SendAll(
            new NetworkRequestLeaveBattle(partyBaseId, finishLocalMenus: false)), MapEventDisabledMethods);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject<TroopUpgradeTracker>(trackerId, out var tracker));

            Assert.Null(party.MapEvent);
            Assert.Same(tracker, mapEvent.TroopUpgradeTracker);
            Assert.Empty(tracker._mapEventParties);
        });

        client.SimulateMessage(Server.NetPeer, new NetworkAddBattleParty(attackerSideId, mapEventPartyId));

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.Same(mapEvent, party.MapEvent);

            var partyIds = new List<string>();
            foreach (var involvedParty in mapEvent._sides.SelectMany(side => side.Parties))
            {
                Assert.True(client.ObjectManager.TryGetId(involvedParty, out var involvedPartyId));
                partyIds.Add(involvedPartyId);
            }
            involvedPartyIds = partyIds.ToArray();
        });

        Assert.NotNull(involvedPartyIds);
        client.SimulateMessage(Server.NetPeer, new NetworkAddInvolvedParties(
            mapEventContext.MapEventId,
            involvedPartyIds,
            new CampaignVec2[involvedPartyIds.Length]));
        client.SimulateMessage(Server.NetPeer, new NetworkAddInvolvedParties(
            mapEventContext.MapEventId,
            involvedPartyIds,
            new CampaignVec2[involvedPartyIds.Length]));

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject<TroopUpgradeTracker>(trackerId, out var tracker));

            Assert.Same(mapEvent, party.MapEvent);
            Assert.Same(tracker, mapEvent.TroopUpgradeTracker);
            AssertTrackerMatchesSides(mapEvent);
            Assert.Contains(tracker._mapEventParties, involvedParty => involvedParty.Party == party.Party);
        });
    }

    [Fact]
    public void JoinActiveSiege_WaitsForAuthoritativePartyAttachmentBeforeOpeningEncounter()
    {
        var mapEventContext = CreateServerMapEvent();
        var (heroId, partyId) = CreatePlayerHeroParty("PlayerOne");
        var client = Clients.First();
        RegisterAsPlayerParty("PlayerOne", heroId, partyId);
        TestEnvironment.ConnectRegisteredPlayer(client, "PlayerOne");
        SetMainParty(client, partyId);
        var siegeEventId = SetClientOnlyCamp(client, partyId);

        client.NetworkSentMessages.Clear();
        var disabledMethods = MapEventDisabledMethods
            .Append(AccessTools.Method(
                typeof(E2E.Tests.Environment.TestNetworkRouter),
                nameof(E2E.Tests.Environment.TestNetworkRouter.SendAll),
                new[] { typeof(LiteNetLib.NetPeer), typeof(IMessage) }))
            .ToList();

        using var menuSwitchRecorder = new GameMenuSwitchRecorder();
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject<SiegeEvent>(siegeEventId, out var siegeEvent));

            mapEvent._mapEventType = MapEvent.BattleTypes.Siege;
            var settlement = siegeEvent.BesiegedSettlement;
            settlement.SiegeEvent = siegeEvent;
            settlement.Party._mapEventSide = mapEvent.DefenderSide;
            mapEvent.DefenderSide.LeaderParty = settlement.Party;
            mapEvent.MapEventSettlement = settlement;
            party._currentSettlement = settlement;

            var encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
            encounter._mapEvent = mapEvent;
            encounter._encounteredParty = mapEvent.DefenderSide.LeaderParty;
            Campaign.Current.PlayerEncounter = encounter;

            var mapState = Game.Current.GameStateManager.CreateState<MapState>();
            mapState._menuContext = ObjectHelper.SkipConstructor<MenuContext>();
            mapState._menuContext.GameMenu = new GameMenu("join_siege_event");
            Game.Current.GameStateManager._gameStates.Add(mapState);

            Assert.False(InvokeJoinSiegeConsequencePrefix());
            Assert.Same(mapEvent, PlayerEncounter.Battle);
            Assert.Null(party.MapEvent);
        }, disabledMethods);

        Assert.Empty(menuSwitchRecorder.SwitchesFor(client));
        var request = Assert.Single(client.NetworkSentMessages.GetMessages<NetworkRequestJoinBattle>());
        Assert.Equal(mapEventContext.MapEventId, request.MapEventId);
        Assert.Equal(BattleSideEnum.Attacker, request.Side);

        Server.NetworkSentMessages.Clear();
        using (var networkDeliveryBlocker = new NetworkDeliveryBlocker())
        {
            Server.SimulateMessage(client.NetPeer, request);
        }

        Assert.True(Server.NetworkSentMessages.GetMessages<NetworkJoinBattleReply>().Single().Accepted);
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkAddBattleParty>());
        var involvedParties = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkAddInvolvedParties>());

        Assert.Empty(menuSwitchRecorder.SwitchesFor(client));

        client.SimulateMessage(Server.NetPeer, involvedParties);
        Assert.Equal(new[] { "encounter" }, menuSwitchRecorder.SwitchesFor(client));
        client.SimulateMessage(Server.NetPeer, Server.NetworkSentMessages.GetMessages<NetworkJoinBattleReply>().Single());
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.Same(mapEvent, party.MapEvent);
        }, MapEventDisabledMethods);
    }

    [Fact]
    public void JoinActiveSiege_DefersInitialMenuActivationUntilAuthoritativeSnapshot()
    {
        var mapEventContext = CreateServerMapEvent();
        var (heroId, partyId) = CreatePlayerHeroParty("PlayerOne");
        var client = Clients.First();
        RegisterAsPlayerParty("PlayerOne", heroId, partyId);
        TestEnvironment.ConnectRegisteredPlayer(client, "PlayerOne");
        SetMainParty(client, partyId);
        var siegeEventId = SetClientOnlyCamp(client, partyId);

        client.NetworkSentMessages.Clear();
        var disabledMethods = MapEventDisabledMethods
            .Append(AccessTools.Method(
                typeof(E2E.Tests.Environment.TestNetworkRouter),
                nameof(E2E.Tests.Environment.TestNetworkRouter.SendAll),
                new[] { typeof(LiteNetLib.NetPeer), typeof(IMessage) }))
            .ToList();

        using var menuActivationRecorder = new GameMenuActivationRecorder();
        using var menuSwitchRecorder = new GameMenuSwitchRecorder();
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject<SiegeEvent>(siegeEventId, out var siegeEvent));

            mapEvent._mapEventType = MapEvent.BattleTypes.Siege;
            var settlement = siegeEvent.BesiegedSettlement;
            settlement.SiegeEvent = siegeEvent;
            settlement.Party._mapEventSide = mapEvent.DefenderSide;
            mapEvent.DefenderSide.LeaderParty = settlement.Party;
            mapEvent.MapEventSettlement = settlement;
            party._currentSettlement = settlement;

            var encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
            encounter._mapEvent = mapEvent;
            encounter._encounteredParty = mapEvent.DefenderSide.LeaderParty;
            Campaign.Current.PlayerEncounter = encounter;

            Assert.True(InvokeActivateJoinSiegeMenuPrefix());
            PlayerEncounter.JoinBattle(BattleSideEnum.Attacker);
            Assert.False(InvokeActivateJoinSiegeMenuPrefix());
            Assert.Null(party.MapEvent);
        }, disabledMethods);

        Assert.Empty(menuActivationRecorder.ActivationsFor(client));
        var request = Assert.Single(client.NetworkSentMessages.GetMessages<NetworkRequestJoinBattle>());

        Server.NetworkSentMessages.Clear();
        using (var networkDeliveryBlocker = new NetworkDeliveryBlocker())
        {
            Server.SimulateMessage(client.NetPeer, request);
        }

        Assert.True(Server.NetworkSentMessages.GetMessages<NetworkJoinBattleReply>().Single().Accepted);
        var involvedParties = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkAddInvolvedParties>());
        Assert.Empty(menuActivationRecorder.ActivationsFor(client));

        client.SimulateMessage(Server.NetPeer, involvedParties);
        Assert.Equal(new[] { "join_siege_event" }, menuActivationRecorder.ActivationsFor(client));
        Assert.Equal(new[] { "encounter" }, menuSwitchRecorder.SwitchesFor(client));
        client.SimulateMessage(Server.NetPeer, Server.NetworkSentMessages.GetMessages<NetworkJoinBattleReply>().Single());
    }

    [Fact]
    public void DeferredJoinMenu_WhenEncounterChanges_ConsumesSnapshotWithoutActivation()
    {
        var mapEventContext = CreateServerMapEvent();
        var (_, partyId) = CreatePlayerHeroParty("PlayerOne");
        var client = Clients.First();
        SetMapEventType(mapEventContext.MapEventId, MapEvent.BattleTypes.Siege);
        SetMainParty(client, partyId);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));

            var encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
            encounter._mapEvent = mapEvent;
            Campaign.Current.PlayerEncounter = encounter;

            var activationGate = client.Container.Resolve<ISiegeJoinMenuActivationGate>();
            activationGate.ArmJoinRequest(mapEvent, party.Party);
            Assert.True(activationGate.TryDeferActivation());

            Campaign.Current.PlayerEncounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
            Assert.True(activationGate.ResumeAfterSnapshot(mapEvent));
        }, MapEventDisabledMethods);
    }

    [Fact]
    public void PromptSiegeAssault_WhenEncounterAlreadyOwnsAssault_PreservesEncounterAndPartySide()
    {
        var mapEventContext = CreateServerMapEvent();
        var partyId = JoinNewServerPartyToSide(mapEventContext.MapEventId, BattleSideEnum.Attacker);
        var client = Clients.First();

        SetMapEventType(mapEventContext.MapEventId, MapEvent.BattleTypes.Siege);
        SetMainParty(client, partyId);
        var siegeEventId = SetClientOnlyCamp(client, partyId);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject<SiegeEvent>(siegeEventId, out var siegeEvent));
            var settlement = siegeEvent.BesiegedSettlement;
            settlement.Party._mapEventSide = mapEvent.DefenderSide;
            mapEvent.MapEventSettlement = settlement;

            PlayerEncounter.Start();
            PlayerEncounter.Current._mapEvent = mapEvent;
            var encounter = PlayerEncounter.Current;

            new SiegeEventInterface().PromptSiegeAssault(party, settlement);

            Assert.Same(encounter, PlayerEncounter.Current);
            Assert.Same(mapEvent, PlayerEncounter.Battle);
            Assert.Same(mapEvent.AttackerSide, party.Party.MapEventSide);
        });
    }

    [Fact]
    public void WoundedNonInitiator_WhenSiegeMissionStarts_LeavesSiege()
    {
        var mapEventContext = CreateServerMapEvent();
        var (_, initiatingPartyId) = CreatePlayerHeroParty("InitiatingPlayer");
        var (woundedHeroId, woundedPartyId) = CreatePlayerHeroParty("WoundedPlayer");
        var woundedClient = Clients.Last();
        string? woundedPartyBaseId = null;

        var disabledMethods = MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(CultureObject), nameof(CultureObject.HasFeat)))
            .Append(AccessTools.Method(typeof(MobileParty), nameof(MobileParty.OnPartyLeftSiegeInternal)))
            .Append(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ExitToLast)))
            .ToList();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(initiatingPartyId, out var initiatingParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(woundedPartyId, out var woundedParty));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(woundedHeroId, out var woundedHero));

            initiatingParty.Party.MapEventSide = mapEvent.AttackerSide;
            woundedParty.Party.MapEventSide = mapEvent.AttackerSide;
            woundedHero.HitPoints = 1;
            Assert.True(woundedHero.IsWounded);
            Assert.True(Server.ObjectManager.TryGetId(woundedParty.Party, out woundedPartyBaseId));
        }, disabledMethods);

        SetMapEventType(mapEventContext.MapEventId, MapEvent.BattleTypes.Siege);
        SetMainParty(woundedClient, woundedPartyId);
        var siegeEventId = SetClientOnlyCamp(woundedClient, woundedPartyId);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<SiegeEvent>(siegeEventId, out var siegeEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(woundedPartyId, out var woundedParty));
            woundedParty._besiegerCamp = siegeEvent.BesiegerCamp;
        });
        woundedClient.Call(() =>
        {
            PlayerEncounter.Start();
            PlayerEncounter.Init();
            Assert.NotNull(PlayerSiege.PlayerSiegeEvent);
            Assert.NotNull(PlayerEncounter.Current);
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            using var handler = new BattleMissionStartHandler(
                Server.Resolve<IMessageBroker>(),
                Server.ObjectManager,
                Server.Resolve<IPlayerManager>(),
                Server.Resolve<INetwork>(),
                Server.Resolve<IMapEventLogger>(),
                Server.Resolve<IBattleMissionInitializerResolver>());

            Assert.True(handler.RemoveWoundedNonInitiatorParties(mapEvent, initiatingPartyId));
        }, disabledMethods);

        Assert.NotNull(woundedPartyBaseId);
        var left = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkPartyLeftBattle>());
        Assert.Equal(woundedPartyBaseId, left.PartyId);
        Assert.True(left.LeaveSiege);

        AssertPartyState(Server, woundedPartyId, expectMapEvent: false, expectCamp: false);
        foreach (var client in Clients)
        {
            AssertPartyState(client, woundedPartyId, expectMapEvent: false, expectCamp: false);
        }
        woundedClient.Call(() =>
        {
            Assert.Null(PlayerSiege.PlayerSiegeEvent);
            Assert.Null(PlayerEncounter.Current);
            Assert.Equal(AiBehavior.Hold, MobileParty.MainParty.DefaultBehavior);
        });
    }

    [Theory]
    [InlineData(MapEvent.BattleTypes.FieldBattle, BattleSideEnum.Attacker)]
    [InlineData(MapEvent.BattleTypes.Siege, BattleSideEnum.Defender)]
    public void WoundedNonInitiator_OutsideAttackingSiege_DoesNotLeaveSiege(
        MapEvent.BattleTypes battleType,
        BattleSideEnum woundedSide)
    {
        var mapEventContext = CreateServerMapEvent();
        var (_, initiatingPartyId) = CreatePlayerHeroParty("InitiatingPlayer");
        var (woundedHeroId, woundedPartyId) = CreatePlayerHeroParty("WoundedPlayer");

        var disabledMethods = MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(CultureObject), nameof(CultureObject.HasFeat)))
            .ToList();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(initiatingPartyId, out var initiatingParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(woundedPartyId, out var woundedParty));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(woundedHeroId, out var woundedHero));

            initiatingParty.Party.MapEventSide = mapEvent.AttackerSide;
            woundedParty.Party.MapEventSide = mapEvent.GetMapEventSide(woundedSide);
            woundedHero.HitPoints = 1;
            Assert.True(woundedHero.IsWounded);
        }, disabledMethods);
        SetMapEventType(mapEventContext.MapEventId, battleType);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            using var handler = new BattleMissionStartHandler(
                Server.Resolve<IMessageBroker>(),
                Server.ObjectManager,
                Server.Resolve<IPlayerManager>(),
                Server.Resolve<INetwork>(),
                Server.Resolve<IMapEventLogger>(),
                Server.Resolve<IBattleMissionInitializerResolver>());

            Assert.True(handler.RemoveWoundedNonInitiatorParties(mapEvent, initiatingPartyId));
        }, disabledMethods);

        var left = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkPartyLeftBattle>());
        Assert.False(left.LeaveSiege);
        AssertPartyState(Server, woundedPartyId, expectMapEvent: false, expectCamp: false);
        foreach (var client in Clients)
        {
            AssertPartyState(client, woundedPartyId, expectMapEvent: false, expectCamp: false);
        }
    }

    private void SetMapEventType(string mapEventId, MapEvent.BattleTypes battleType)
    {
        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
                mapEvent._mapEventType = battleType;
            });
        }
    }

    private string SetClientOnlyCamp(EnvironmentInstance client, string partyId)
    {
        var disabledMethods = SiegeCreationDisabledMethods
            .Append(AccessTools.Method(typeof(PartyBaseHelper), nameof(PartyBaseHelper.HasFeat)))
            .ToList();
        var siegeEventId = TestEnvironment.CreateRegisteredObject<SiegeEvent>(disabledMethods);
        SetCamp(client, partyId, siegeEventId);
        return siegeEventId;
    }

    private static void SetCamp(EnvironmentInstance instance, string partyId, string siegeEventId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<SiegeEvent>(siegeEventId, out var siegeEvent));
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.NotNull(siegeEvent.BesiegerCamp);

            party._besiegerCamp = siegeEvent.BesiegerCamp;
        });
    }

    private static void SetMainParty(EnvironmentInstance instance, string partyId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Campaign.Current.MainParty = party;
        });
    }

    private static void AssertPartyState(
        EnvironmentInstance instance,
        string partyId,
        bool expectMapEvent,
        bool expectCamp)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.Equal(expectMapEvent, party.MapEvent != null);
            Assert.Equal(expectCamp, party.BesiegerCamp != null);
        });
    }

    private static void AssertTrackerMatchesSides(MapEvent mapEvent)
    {
        var involvedParties = mapEvent._sides.SelectMany(side => side.Parties).ToList();
        Assert.Equal(involvedParties.Count, mapEvent.TroopUpgradeTracker._mapEventParties.Count);
        Assert.All(involvedParties, party => Assert.Contains(party, mapEvent.TroopUpgradeTracker._mapEventParties));
    }

    private static bool InvokeJoinSiegeConsequencePrefix()
    {
        var patchType = AccessTools.TypeByName("GameInterface.Services.SiegeEvents.Patches.SiegeEntryFlowPatches");
        var prefix = AccessTools.Method(patchType, "JoinSiegeConsequencePrefix");
        Assert.NotNull(prefix);

        return (bool)prefix.Invoke(null, Array.Empty<object>())!;
    }

    private static bool InvokeActivateJoinSiegeMenuPrefix()
    {
        var patchType = AccessTools.TypeByName("GameInterface.Services.SiegeEvents.Patches.SiegeEntryFlowPatches");
        var prefix = AccessTools.Method(patchType, "ActivateJoinSiegeMenuPrefix");
        Assert.NotNull(prefix);

        return (bool)prefix.Invoke(null, new object[] { "join_siege_event" })!;
    }

    private sealed class GameMenuActivationRecorder : IDisposable
    {
        private static readonly MethodInfo ActivateGameMenuMethod =
            AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ActivateGameMenu), new[] { typeof(string) });
        private static readonly List<(object Container, string MenuId)> ActivationCalls = new();

        private readonly Harmony harmony = new($"siege-join-menu-activation-recorder-{Guid.NewGuid()}");

        public GameMenuActivationRecorder()
        {
            ActivationCalls.Clear();
            harmony.Patch(
                ActivateGameMenuMethod,
                prefix: new HarmonyMethod(typeof(GameMenuActivationRecorder), nameof(RecordActivation))
                {
                    priority = Priority.Last,
                });
        }

        public string[] ActivationsFor(EnvironmentInstance instance) =>
            ActivationCalls
                .Where(call => ReferenceEquals(call.Container, instance.Container))
                .Select(call => call.MenuId)
                .ToArray();

        public void Dispose() =>
            harmony.Unpatch(ActivateGameMenuMethod, HarmonyPatchType.Prefix, harmony.Id);

        private static bool RecordActivation(string menuId)
        {
            if (GameInterface.ContainerProvider.TryGetContainer(out var container))
                ActivationCalls.Add((container, menuId));
            return false;
        }
    }

    private sealed class GameMenuSwitchRecorder : IDisposable
    {
        private static readonly MethodInfo SwitchToMenuMethod =
            AccessTools.Method(typeof(GameMenu), nameof(GameMenu.SwitchToMenu), new[] { typeof(string) });
        private static readonly List<(object Container, string MenuId)> SwitchCalls = new();

        private readonly Harmony harmony = new($"siege-join-menu-recorder-{Guid.NewGuid()}");

        public GameMenuSwitchRecorder()
        {
            SwitchCalls.Clear();
            harmony.Patch(
                SwitchToMenuMethod,
                prefix: new HarmonyMethod(typeof(GameMenuSwitchRecorder), nameof(RecordSwitchToMenu))
                {
                    priority = Priority.First,
                });
        }

        public string[] SwitchesFor(EnvironmentInstance instance) =>
            SwitchCalls
                .Where(call => ReferenceEquals(call.Container, instance.Container))
                .Select(call => call.MenuId)
                .ToArray();

        public void Dispose() =>
            harmony.Unpatch(SwitchToMenuMethod, HarmonyPatchType.Prefix, harmony.Id);

        private static bool RecordSwitchToMenu(string menuId)
        {
            if (GameInterface.ContainerProvider.TryGetContainer(out var container))
                SwitchCalls.Add((container, menuId));
            return false;
        }
    }

    private sealed class NetworkDeliveryBlocker : IDisposable
    {
        private static readonly MethodInfo[] DeliveryMethods =
        {
            AccessTools.Method(
                typeof(E2E.Tests.Environment.TestNetworkRouter),
                nameof(E2E.Tests.Environment.TestNetworkRouter.Send),
                new[] { typeof(LiteNetLib.NetPeer), typeof(LiteNetLib.NetPeer), typeof(IMessage) }),
            AccessTools.Method(
                typeof(E2E.Tests.Environment.TestNetworkRouter),
                nameof(E2E.Tests.Environment.TestNetworkRouter.SendAll),
                new[] { typeof(LiteNetLib.NetPeer), typeof(IMessage) }),
        };

        private readonly Harmony harmony = new($"siege-join-network-blocker-{Guid.NewGuid()}");

        public NetworkDeliveryBlocker()
        {
            var prefix = new HarmonyMethod(typeof(NetworkDeliveryBlocker), nameof(Block));
            foreach (var method in DeliveryMethods)
                harmony.Patch(method, prefix: prefix);
        }

        public void Dispose()
        {
            foreach (var method in DeliveryMethods)
                harmony.Unpatch(method, HarmonyPatchType.Prefix, harmony.Id);
        }

        private static bool Block(IMessage message) =>
            message is not NetworkAddInvolvedParties &&
            message is not NetworkJoinBattleReply;
    }
}
