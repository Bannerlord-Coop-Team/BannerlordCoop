using Common.Messaging;
using Autofac;
using Common.Logging;
using Common.Network;
using Common.PacketHandlers;
using Common.Serialization;
using Common.Util;
using Coop.Core.Client.Services.SiegeEvents.Messages;
using Coop.Core.Server.Services.SiegeEvents.Messages;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.MapEvents;
using E2E.Tests.Util;
using GameInterface.Tests.Utils;
using GameInterface.Configuration;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Extensions;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Interfaces;
using GameInterface.Services.MapEvents.Logging;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.MapEvents.Patches;
using GameInterface.Services.MapEventSides.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.SiegeEvents;
using GameInterface.Services.SiegeEvents.Interfaces;
using GameInterface.Services.SiegeEvents.Messages;
using HarmonyLib;
using Helpers;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.SiegeEvents;

public class SiegeAssaultLeaveTests : MapEventTestBase
{
    public SiegeAssaultLeaveTests(ITestOutputHelper output) : base(output)
    {
    }

    private static IEnumerable<MethodBase> SiegeLeaveDisabledMethods => SiegeCreationDisabledMethods
        .Append(AccessTools.Method(typeof(CultureObject), nameof(CultureObject.HasFeat)));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void JoinActiveSiege_AssignsCampOnlyAfterServerAccepts(bool rejectJoin)
    {
        var battle = CreateServerMapEvent();
        var (heroId, partyId) = CreatePlayerHeroParty("PlayerOne");
        var client = Clients.First();
        RegisterAsPlayerParty("PlayerOne", heroId, partyId);
        TestEnvironment.ConnectRegisteredPlayer(client, "PlayerOne");
        SetMainParty(client, partyId);
        var disabledMethods = MapEventDisabledMethods.Concat(SiegeLeaveDisabledMethods)
            .Append(AccessTools.Method(typeof(PartyBaseHelper), nameof(PartyBaseHelper.HasFeat)))
            .ToList();
        var siegeId = TestEnvironment.CreateRegisteredObject<SiegeEvent>(disabledMethods);
        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MapEvent>(battle.MapEventId, out var mapEvent));
                Assert.True(instance.ObjectManager.TryGetObject<SiegeEvent>(siegeId, out var siege));
                mapEvent.SetBattleType(MapEvent.BattleTypes.Siege);
                mapEvent.SetMapEventSettlement(siege.BesiegedSettlement);
                siege.BesiegedSettlement.SiegeEvent = siege;
                siege.BesiegedSettlement.Party._mapEventSide = mapEvent.DefenderSide;
                mapEvent.DefenderSide.LeaderParty = siege.BesiegedSettlement.Party;
            }, disabledMethods);
        }

        var blockedDelivery = AccessTools.Method(
            typeof(E2E.Tests.Environment.TestNetworkRouter),
            nameof(E2E.Tests.Environment.TestNetworkRouter.SendReliablePayload),
            new[] { typeof(LiteNetLib.NetPeer), typeof(LiteNetLib.NetPeer), typeof(byte[]) });
        client.NetworkSentMessages.Clear();
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(battle.MapEventId, out var mapEvent));
            var encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
            encounter._encounteredParty = mapEvent.DefenderSide.LeaderParty;
            encounter._mapEvent = mapEvent;
            Campaign.Current.PlayerEncounter = encounter;
            PlayerEncounter.JoinBattle(BattleSideEnum.Attacker);
            Assert.True(encounter.IsJoinedBattle);
            Assert.Null(MobileParty.MainParty.MapEvent);
            Assert.Null(MobileParty.MainParty.BesiegerCamp);
        }, disabledMethods.Append(blockedDelivery).ToList());

        var request = Assert.Single(client.NetworkSentMessages.GetMessages<NetworkRequestJoinBattle>());
        Server.NetworkSentMessages.Clear();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(battle.MapEventId, out var mapEvent));
            if (rejectJoin) mapEvent._state = MapEventState.WaitingRemoval;
            Server.Resolve<IMessageBroker>().Publish(client.NetPeer, request);
        }, disabledMethods);

        Assert.Equal(!rejectJoin, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkJoinBattleReply>()).Accepted);
        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                Assert.True(instance.ObjectManager.TryGetObject<SiegeEvent>(siegeId, out var siege));
                Assert.Equal(!rejectJoin, party.MapEvent != null);
                Assert.Same(rejectJoin ? null : siege.BesiegerCamp, party.BesiegerCamp);
            });
        }

        if (rejectJoin)
        {
            client.Call(() => Assert.False(PlayerEncounter.Current.IsJoinedBattle));
            return;
        }

        client.Call(() => client.Resolve<INetwork>().SendAll(
            new NetworkRequestBreakSiege(partyId, finishLocalMenus: true)),
            disabledMethods.Append(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ExitToLast))).ToList());
        foreach (var instance in Clients.Append(Server))
            AssertPartyState(instance, partyId, expectMapEvent: false, expectCamp: false);
    }

    // No-camp attacker is covered by SiegeDisconnectTests.BreakSiegeRequest_WithNoCampDuringAssault_*.
    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void ActiveBesieger_WithOrWithoutServerCamp_CanLeave(bool serverHasCamp, bool sallyOut, bool lastBesieger)
    {
        var mapEvent = CreateServerMapEvent();
        var partyId = lastBesieger ? mapEvent.DefenderPartyId :
            JoinNewServerPartyToSide(mapEvent.MapEventId,
                sallyOut ? BattleSideEnum.Defender : BattleSideEnum.Attacker);
        SetMapEventType(mapEvent.MapEventId,
            sallyOut ? MapEvent.BattleTypes.SallyOut : MapEvent.BattleTypes.Siege);
        if (sallyOut)
        {
            // A sally out finalizes through its component, which reads the besieged settlement.
            var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
            foreach (var instance in Clients.Append(Server))
                instance.Call(() =>
                {
                    Assert.True(instance.ObjectManager.TryGetObject<MapEvent>(mapEvent.MapEventId, out var battle));
                    Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
                    battle.SetMapEventSettlement(settlement);
                });
        }
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
        EnableHeadlessEncounterFinish(leavingClient);
        leavingClient.Call(() =>
        {
            Assert.True(leavingClient.ObjectManager.TryGetObject<MapEvent>(mapEvent.MapEventId, out var battle));
            PlayerEncounter.Start();
            PlayerEncounter.Current._mapEvent = battle;
        }, MapEventDisabledMethods);
        Server.NetworkSentMessages.Clear();

        leavingClient.Call(() =>
        {
            leavingClient.Resolve<INetwork>().SendAll(new NetworkRequestBreakSiege(partyId, finishLocalMenus: true));
        }, MapEventDisabledMethods
            .Concat(SiegeLeaveDisabledMethods)
            .Append(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ExitToLast)))
            .ToList());

        var left = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkPartyLeftBattle>());
        Assert.Equal(partyBaseId, left.PartyId);
        Assert.True(left.LeaveSiege);

        var approval = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBreakSiegeApproved>());
        Assert.Equal(SiegeBreakOutcome.Applied, approval.Outcome);
        Assert.True(approval.BattleLeaveApplied);
        leavingClient.Call(() => Assert.Null(PlayerEncounter.Current));

        AssertPartyState(Server, partyId, expectMapEvent: false, expectCamp: false);
        foreach (var client in Clients)
        {
            AssertPartyState(client, partyId, expectMapEvent: false, expectCamp: false);
        }
        foreach (var instance in Clients.Append(Server))
            AssertPartyState(instance, mapEvent.AttackerPartyId, expectMapEvent: !lastBesieger, expectCamp: false);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LeaveSoldiersBehindAccept_InSiegeBattle_SacrificesBeforeLeaving(bool sallyOut)
    {
        var mapEvent = CreateServerMapEvent();
        var partyId = JoinNewServerPartyToSide(mapEvent.MapEventId,
            sallyOut ? BattleSideEnum.Defender : BattleSideEnum.Attacker);
        SetMapEventType(mapEvent.MapEventId,
            sallyOut ? MapEvent.BattleTypes.SallyOut : MapEvent.BattleTypes.Siege);
        var leavingClient = Clients.First();
        SetMainParty(leavingClient, partyId);
        var heroId = SetMainHero(leavingClient);
        RegisterAsPlayerParty("PlayerOne", heroId, partyId);
        TestEnvironment.ConnectRegisteredPlayer(leavingClient, "PlayerOne");
        var siegeEventId = SetClientOnlyCamp(leavingClient, partyId);
        SetCamp(Server, partyId, siegeEventId);
        AddEncounterGameMenuBehavior(Server);

        using var recorder = new LeftSoldiersBehindRecorder();
        leavingClient.Call(InvokePatchedLeaveSoldiersBehindAccept, LeaveSoldiersBehindDisabledMethods);

        // The sacrifice goes out ahead of the siege break, so the server applies it while the party is still in its battle.
        var sent = leavingClient.NetworkSentMessages.ToList();
        int sacrificeIndex = sent.FindIndex(message => message is NetworkPlayerLeftSoldiersBehind);
        int breakIndex = sent.FindIndex(message => message is NetworkRequestBreakSiege);
        Assert.NotEqual(-1, sacrificeIndex);
        Assert.NotEqual(-1, breakIndex);
        Assert.True(sacrificeIndex < breakIndex);

        Assert.Equal(new[] { true }, recorder.InMapEvent);
        AssertPartyState(Server, partyId, expectMapEvent: false, expectCamp: false);
    }

    [Fact]
    public void RepeatedLeaveSoldiersBehind_SameMapEvent_SacrificesOnce()
    {
        var mapEvent = CreateServerMapEvent();
        var partyId = JoinNewServerPartyToSide(mapEvent.MapEventId, BattleSideEnum.Attacker);
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        AddEncounterGameMenuBehavior(Server);
        var client = Clients.First();
        RegisterAsPlayerParty("PlayerOne", heroId, partyId);
        TestEnvironment.ConnectRegisteredPlayer(client, "PlayerOne");

        using var recorder = new LeftSoldiersBehindRecorder();
        client.Call(() =>
        {
            var network = client.Resolve<INetwork>();
            network.SendAll(new NetworkPlayerLeftSoldiersBehind(heroId, partyId));
            network.SendAll(new NetworkPlayerLeftSoldiersBehind(heroId, partyId));
        }, MapEventDisabledMethods);

        Assert.Equal(new[] { true }, recorder.InMapEvent);
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

        client.SimulateMessage(Server.NetPeer, new NetworkAddBattleParty(attackerSideId, mapEventPartyId, partyBaseId));

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

    [Theory]
    [InlineData("encounter", "absent", false)]
    [InlineData("join_siege_event", "absent", false)]
    [InlineData("join_siege_event", "empty", false)]
    [InlineData("join_siege_event", "settlement", true)]
    [InlineData("join_siege_event", "matching", true)]
    [InlineData("join_siege_event", "other-battle", false)]
    public void InvolvedParties_PreservesSnapshotAndOnlyResumesMatchingJoinMenu(
        string menuId, string encounterState, bool expectMenuSwitch)
    {
        var context = CreateServerMapEvent();
        var partyId = JoinNewServerPartyToSide(context.MapEventId, BattleSideEnum.Attacker);
        var client = Clients.First();
        SetMapEventType(context.MapEventId, MapEvent.BattleTypes.Siege);
        SetMainParty(client, partyId);
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        string[] involvedPartyIds = Array.Empty<string>();
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(context.MapEventId, out var mapEvent));
            var encounter = encounterState == "absent" ? null : ObjectHelper.SkipConstructor<PlayerEncounter>();
            Campaign.Current.PlayerEncounter = encounter;
            if (encounterState == "matching")
                encounter!._mapEvent = mapEvent;
            if (encounterState == "other-battle")
                encounter!._mapEvent = ObjectHelper.SkipConstructor<MapEvent>();
            if (encounterState == "settlement")
            {
                Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
                settlement.SiegeEvent = null;
                settlement.Party._mapEventSide = null;
                encounter!._encounteredParty = settlement.Party;
            }

            OpenClientMenu(menuId);
            involvedPartyIds = mapEvent._sides.SelectMany(side => side.Parties).Select(party =>
            {
                Assert.True(client.ObjectManager.TryGetId(party, out var id));
                return id;
            }).ToArray();
        });

        int applyFailures = 0;
        Action<string> captureFailure = message =>
        {
            if (message.Contains("Failed to apply") && message.Contains("NetworkAddInvolvedParties"))
                Interlocked.Increment(ref applyFailures);
        };
        using var menuSwitchRecorder = CreateMenuSwitchRecorder();
        OutputSinkManager.AddLogCallback(captureFailure);
        try
        {
            client.SimulateMessage(Server.NetPeer, new NetworkAddInvolvedParties(
                context.MapEventId, involvedPartyIds, new CampaignVec2[involvedPartyIds.Length]));
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<MapEvent>(context.MapEventId, out var mapEvent));
                AssertTrackerMatchesSides(mapEvent);
            });
            Assert.Equal(0, Volatile.Read(ref applyFailures));
            Assert.Equal(expectMenuSwitch ? new[] { "encounter" } : Array.Empty<string>(),
                menuSwitchRecorder.MenusFor(client));
        }
        finally
        {
            OutputSinkManager.RemoveLogCallback(captureFailure);
        }
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
                nameof(E2E.Tests.Environment.TestNetworkRouter.SendReliablePayload),
                new[] { typeof(LiteNetLib.NetPeer), typeof(LiteNetLib.NetPeer), typeof(byte[]) }))
            .ToList();

        using var menuSwitchRecorder = CreateMenuSwitchRecorder();
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject<SiegeEvent>(siegeEventId, out var siegeEvent));

            StageSettlementSiege(mapEvent, siegeEvent, party);

            var encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
            encounter._mapEvent = mapEvent;
            encounter._encounteredParty = mapEvent.DefenderSide.LeaderParty;
            Campaign.Current.PlayerEncounter = encounter;

            OpenClientMenu("join_siege_event");

            Assert.False(InvokeJoinSiegeConsequencePrefix());
            Assert.Same(mapEvent, PlayerEncounter.Battle);
            Assert.Null(party.MapEvent);
        }, disabledMethods);

        Assert.Empty(menuSwitchRecorder.MenusFor(client));
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

        Assert.Empty(menuSwitchRecorder.MenusFor(client));

        client.SimulateMessage(Server.NetPeer, involvedParties);
        Assert.Equal(new[] { "encounter" }, menuSwitchRecorder.MenusFor(client));
        client.SimulateMessage(Server.NetPeer, Server.NetworkSentMessages.GetMessages<NetworkJoinBattleReply>().Single());
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.Same(mapEvent, party.MapEvent);

            Assert.Contains(
                mapEvent.DefenderSide.Parties.SelectMany(mapEventParty => mapEventParty.Troops),
                troop => !troop.IsWounded && !troop.IsKilled);
            foreach (var mapEventParty in mapEvent.DefenderSide.Parties)
                mapEventParty.Party.MemberRoster.Clear();

            var nativeArgs = new MenuCallbackArgs((MenuContext)null, null);
            Assert.False(MenuHelper.EncounterAttackCondition(nativeArgs));
            Assert.False(MenuHelper.EncounterOrderAttackCondition(nativeArgs));

            var (shown, args) = InvokePatchedEncounterAttackCondition();
            Assert.True(shown);
            Assert.True(args.IsEnabled);

            var (sendTroopsShown, sendTroopsArgs) = InvokePatchedEncounterOrderAttackCondition();
            Assert.True(sendTroopsShown);
            Assert.True(sendTroopsArgs.IsEnabled);
            Assert.Equal(GameMenuOption.LeaveType.OrderTroopsToAttack, sendTroopsArgs.optionLeaveType);

            var healthyHitPoints = Hero.MainHero.HitPoints;
            var previousOptions = ModConfigProvider.ModOptions;
            try
            {
                using (new AllowedThread())
                    Hero.MainHero.HitPoints = 1;

                ModConfigProvider.ModOptions = new ModOptions(new ModOptionsData
                {
                    PlayerWoundedBattleEntry = false,
                });
                (shown, args) = InvokePatchedEncounterAttackCondition();
                Assert.True(shown);
                Assert.False(args.IsEnabled);

                ModConfigProvider.ModOptions = new ModOptions(new ModOptionsData
                {
                    PlayerWoundedBattleEntry = true,
                });
                (shown, args) = InvokePatchedEncounterAttackCondition();
                Assert.True(shown);
                Assert.True(args.IsEnabled);
            }
            finally
            {
                ModConfigProvider.ModOptions = previousOptions;
                using (new AllowedThread())
                    Hero.MainHero.HitPoints = healthyHitPoints;
            }
        }, MapEventDisabledMethods);
    }

    [Fact]
    public void OrderAttackFallback_PreservesVisibilityLeaveTypeAndText()
    {
        var battle = CreateServerMapEvent();
        var allyId = JoinNewServerPartyToSide(battle.MapEventId, BattleSideEnum.Attacker);
        var troopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        var client = Clients.First();
        SetMainParty(client, battle.AttackerPartyId);
        var disabledMethods = MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(MobilePartyHelper), nameof(MobilePartyHelper.CanPartyAttackWithCurrentMorale)))
            .ToList();

        client.Call(() =>
        {
            var mapEvent = client.GetRegisteredObject<MapEvent>(battle.MapEventId);
            var attacker = client.GetRegisteredObject<MobileParty>(battle.AttackerPartyId);
            var defender = client.GetRegisteredObject<MobileParty>(battle.DefenderPartyId);
            var ally = client.GetRegisteredObject<MobileParty>(allyId);
            var encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
            encounter._mapEvent = mapEvent;
            encounter._encounteredParty = defender.Party;
            encounter.PlayerSide = BattleSideEnum.Attacker;
            encounter.OpponentSide = BattleSideEnum.Defender;
            Campaign.Current.PlayerEncounter = encounter;

            var attackerSide = mapEvent.AttackerSide;
            var defenderSide = mapEvent.DefenderSide;
            var troop = client.GetRegisteredObject<CharacterObject>(troopId);
            const string unchanged = "unset order attack text";
            const string sendTroops = "Send troops.";
            const string leaveToOthers = "Leave it to the others.";
            const string originalTooltip = "unset order attack tooltip";
            const string lowMoraleTooltip = "Your men lack the courage to continue the battle without you. (Low Morale)";
            var sendType = GameMenuOption.LeaveType.OrderTroopsToAttack;
            var untouchedType = new MenuCallbackArgs((MenuContext)null, null).optionLeaveType;

            (string Name, bool Attached, bool DefenderRaft, bool DefenderHasTroops, bool HasLeader, bool PlayerOnly, bool Naval, bool AttackerRaft,
                bool CanOrder, bool Shown, GameMenuOption.LeaveType LeaveType, string Text)[] rows =
            {
                (Name: "attached", Attached: true, DefenderRaft: false, DefenderHasTroops: true, HasLeader: true, PlayerOnly: false, Naval: false, AttackerRaft: false,
                    CanOrder: true, Shown: true, LeaveType: sendType, Text: sendTroops),
                ("unattached", false, false, true, true, false, false, false, false, false, untouchedType, unchanged),
                ("opponent in raft", true, true, true, true, false, false, false, false, false, sendType, unchanged),
                ("opponent in raft with no troops", true, true, false, true, false, false, false, false, false, sendType, unchanged),
                ("no opposing troops", true, false, false, true, false, false, false, false, true, sendType, sendTroops),
                ("no opposing leader", true, false, true, false, false, false, false, false, true, sendType, sendTroops),
                ("no orderable troops", true, false, true, true, true, false, false, false, false, sendType, unchanged),
                ("player alone with no opposing troops", true, false, false, true, true, false, false, false, false, sendType, unchanged),
                ("naval", true, false, true, true, false, true, false, true, true, GameMenuOption.LeaveType.OrderShipsToAttack, "Send ships."),
                ("player in raft with an ally able to fight", true, false, true, true, false, false, true, true, true, sendType, leaveToOthers),
            };

            using var allowed = new AllowedThread();
            var orderAttackRule = client.Resolve<IOrderAttackRule>();
            var defenderFaction = defender.MapFaction;
            var originalProtection = defenderFaction.NotAttackableByPlayerUntilTime;
            foreach (var row in rows)
            {
                mapEvent.Position = new CampaignVec2(default, isOnLand: !row.Naval);
                attacker.Party._mapEventSide = row.Attached ? attackerSide : null;
                // During the join window vanilla hasn't set the party's side or added it to the event.
                var attackerEntry = Assert.Single(attackerSide.Parties, entry => entry.Party == attacker.Party);
                if (!row.Attached)
                    attackerSide._battleParties.Remove(attackerEntry);
                defender.IsInRaftState = row.DefenderRaft;
                attacker.IsInRaftState = row.AttackerRaft;
                defenderSide.LeaderParty = row.HasLeader ? defender.Party : null;
                attacker.MemberRoster.Clear();
                defender.MemberRoster.Clear();
                ally.MemberRoster.Clear();
                attacker.MemberRoster.AddToCounts(row.PlayerOnly ? CharacterObject.PlayerCharacter : troop, 1);
                if (row.DefenderHasTroops)
                    defender.MemberRoster.AddToCounts(troop, 1);
                if (row.AttackerRaft)
                    ally.MemberRoster.AddToCounts(troop, 1);

                var tooltip = new TextObject(originalTooltip);
                // A protected opponent makes the honor check write both fields, even with no orderable troops.
                if (row.PlayerOnly)
                {
                    defenderFaction.NotAttackableByPlayerUntilTime = CampaignTime.DaysFromNow(1f);
                    var honorArgs = new MenuCallbackArgs((MenuContext)null, null) { IsEnabled = true, Tooltip = tooltip };
                    MenuHelper.CheckEnemyAttackableHonorably(honorArgs);
                    Assert.False(honorArgs.IsEnabled);
                    Assert.NotSame(tooltip, honorArgs.Tooltip);
                }

                MBTextManager.SetTextVariable("SEND_TROOPS_TEXT", unchanged);
                var (shown, args) = InvokeSiegeOrderAttackFallback(tooltip);
                defenderFaction.NotAttackableByPlayerUntilTime = originalProtection;
                var text = new TextObject("{=!}{SEND_TROOPS_TEXT}").ToString();

                Assert.Equal(
                    (row.Name, row.CanOrder, row.Shown, row.LeaveType, row.Text,
                        Enabled: !row.Shown, Tooltip: row.Shown ? lowMoraleTooltip : originalTooltip),
                    (row.Name, orderAttackRule.CanOrderAttack(mapEvent, attacker, CharacterObject.PlayerCharacter),
                        shown, args.optionLeaveType, text, args.IsEnabled, args.Tooltip?.ToString()));
                if (!row.Attached)
                    attackerSide._battleParties.Add(attackerEntry);
            }
        }, disabledMethods);
    }

    private static (bool shown, MenuCallbackArgs args) InvokePatchedEncounterAttackCondition()
    {
        var method = AccessTools.Method(
            typeof(EncounterGameMenuBehavior),
            "game_menu_encounter_attack_on_condition");
        Assert.NotNull(method);

        var args = new MenuCallbackArgs((MenuContext)null, null);
        var behavior = ObjectHelper.SkipConstructor<EncounterGameMenuBehavior>();
        var shown = (bool)method.Invoke(behavior, new object[] { args });
        return (shown, args);
    }

    private static (bool shown, MenuCallbackArgs args) InvokePatchedEncounterOrderAttackCondition()
    {
        var method = AccessTools.Method(
            typeof(EncounterGameMenuBehavior),
            "game_menu_encounter_order_attack_on_condition");
        Assert.NotNull(method);

        var args = new MenuCallbackArgs((MenuContext)null, null);
        var behavior = ObjectHelper.SkipConstructor<EncounterGameMenuBehavior>();
        var shown = (bool)method.Invoke(behavior, new object[] { args });
        return (shown, args);
    }

    private static (bool shown, MenuCallbackArgs args) InvokeSiegeOrderAttackFallback(TextObject tooltip)
    {
        var method = AccessTools.Method(typeof(BattleModeEncounterOptionsPatch), "TryApplyClientSiegeOrderAttackOptionState");
        Assert.NotNull(method);

        var args = new MenuCallbackArgs((MenuContext)null, null) { IsEnabled = true, Tooltip = tooltip };
        var shown = (bool)method.Invoke(null, new object[] { args });
        return (shown, args);
    }

    [Fact]
    public void JoinActiveSiege_DefersInitialMenuActivationUntilAuthoritativeSnapshot()
    {
        var client = Clients.First();
        using var menuActivationRecorder = CreateMenuActivationRecorder();
        using var menuSwitchRecorder = CreateMenuSwitchRecorder();

        var (_, involvedParties, joinReply) = StageDeferredSiegeJoin(client);
        Assert.Empty(menuActivationRecorder.MenusFor(client));

        client.SimulateMessage(Server.NetPeer, involvedParties);
        Assert.Equal(new[] { "join_siege_event" }, menuActivationRecorder.MenusFor(client));
        Assert.Equal(new[] { "encounter" }, menuSwitchRecorder.MenusFor(client));
        client.SimulateMessage(Server.NetPeer, joinReply);
    }

    [Fact]
    public void DeferredJoinMenu_WhenEncounterChanges_SnapshotSkipsActivationAndEncounterSwitch()
    {
        var client = Clients.First();
        using var menuActivationRecorder = CreateMenuActivationRecorder();
        using var menuSwitchRecorder = CreateMenuSwitchRecorder();
        var (mapEventId, involvedParties, joinReply) = StageDeferredSiegeJoin(client);

        // A new encounter on the same battle with join_siege_event open would otherwise take the encounter-switch fallback.
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            var changedEncounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
            changedEncounter._mapEvent = mapEvent;
            changedEncounter._encounteredParty = mapEvent.DefenderSide.LeaderParty;
            Campaign.Current.PlayerEncounter = changedEncounter;

            OpenClientMenu("join_siege_event");
        });
        menuActivationRecorder.Clear();
        menuSwitchRecorder.Clear();

        client.SimulateMessage(Server.NetPeer, involvedParties);
        Assert.Empty(menuActivationRecorder.MenusFor(client));
        Assert.Empty(menuSwitchRecorder.MenusFor(client));
        client.SimulateMessage(Server.NetPeer, joinReply);
    }

    // Only a waiting village join is cancelled by Finish, so an accepted siege join gets no leave from it.
    [Fact]
    public void DeferredSiegeJoin_DontGetInvolvedBeforeReply_SendsNoLeave()
    {
        var client = Clients.First();
        var (_, _, joinReply) = StageDeferredSiegeJoin(client);

        client.Call(() =>
        {
            // A party inside the town has no camp, so "Don't get involved" runs vanilla's PlayerEncounter.Finish.
            MobileParty.MainParty._besiegerCamp = null;
            Campaign.Current.MapStateData ??= new MapStateData();
            ObjectHelper.SkipConstructor<EncounterGameMenuBehavior>().break_in_leave_consequence(new MenuCallbackArgs((MenuContext)null, null));
        }, LeaveSoldiersBehindDisabledMethods);

        client.NetworkSentMessages.Clear();
        client.SimulateMessage(Server.NetPeer, joinReply);
        Assert.Empty(client.NetworkSentMessages.GetMessages<NetworkRequestLeaveBattle>());
    }

    /// <summary>Stages a client siege join whose menu is deferred, and returns the server's undelivered snapshot and reply.</summary>
    private (string mapEventId, NetworkAddInvolvedParties involvedParties, NetworkJoinBattleReply joinReply) StageDeferredSiegeJoin(EnvironmentInstance client)
    {
        var mapEventContext = CreateServerMapEvent();
        var (heroId, partyId) = CreatePlayerHeroParty("PlayerOne");
        RegisterAsPlayerParty("PlayerOne", heroId, partyId);
        TestEnvironment.ConnectRegisteredPlayer(client, "PlayerOne");
        SetMainParty(client, partyId);
        var siegeEventId = SetClientOnlyCamp(client, partyId);

        client.NetworkSentMessages.Clear();
        var disabledMethods = MapEventDisabledMethods
            .Append(AccessTools.Method(
                typeof(E2E.Tests.Environment.TestNetworkRouter),
                nameof(E2E.Tests.Environment.TestNetworkRouter.SendReliablePayload),
                new[] { typeof(LiteNetLib.NetPeer), typeof(LiteNetLib.NetPeer), typeof(byte[]) }))
            .ToList();

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject<SiegeEvent>(siegeEventId, out var siegeEvent));

            StageSettlementSiege(mapEvent, siegeEvent, party);

            var encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
            encounter._mapEvent = mapEvent;
            encounter._encounteredParty = mapEvent.DefenderSide.LeaderParty;
            Campaign.Current.PlayerEncounter = encounter;

            Assert.True(InvokeActivateJoinSiegeMenuPrefix());
            PlayerEncounter.JoinBattle(BattleSideEnum.Attacker);
            Assert.False(InvokeActivateJoinSiegeMenuPrefix());
            Assert.Null(party.MapEvent);
        }, disabledMethods);

        var request = Assert.Single(client.NetworkSentMessages.GetMessages<NetworkRequestJoinBattle>());

        Server.NetworkSentMessages.Clear();
        using (var networkDeliveryBlocker = new NetworkDeliveryBlocker())
        {
            Server.SimulateMessage(client.NetPeer, request);
        }

        var joinReply = Server.NetworkSentMessages.GetMessages<NetworkJoinBattleReply>().Single();
        Assert.True(joinReply.Accepted);
        var involvedParties = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkAddInvolvedParties>());
        return (mapEventContext.MapEventId, involvedParties, joinReply);
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
            mapEvent.SetMapEventSettlement(settlement);

            PlayerEncounter.Start();
            PlayerEncounter.Current._mapEvent = mapEvent;
            var encounter = PlayerEncounter.Current;

            client.Resolve<ISiegeEventInterface>().PromptSiegeAssault(party, settlement);

            Assert.Same(encounter, PlayerEncounter.Current);
            Assert.Same(mapEvent, PlayerEncounter.Battle);
            Assert.Same(mapEvent.AttackerSide, party.Party.MapEventSide);
        });
    }

    // #4858: the defender's Leave only finished the encounter on the client, so the server kept the party in the
    // defense and the battle waited on a player that had already left.
    [Fact]
    public void AssaultStartsWithPlayerInsideTown_DefenderMenuHasNoLeave()
    {
        var battle = CreateServerMapEvent();
        var (_, partyId) = CreatePlayerHeroParty("PlayerOne");
        var client = Clients.First();
        TestEnvironment.ConnectRegisteredPlayer(client, "PlayerOne");
        SetMainParty(client, partyId);
        var disabledMethods = MapEventDisabledMethods.Concat(SiegeLeaveDisabledMethods)
            .Append(AccessTools.Method(typeof(PartyBaseHelper), nameof(PartyBaseHelper.HasFeat)))
            .ToList();
        var siegeId = TestEnvironment.CreateRegisteredObject<SiegeEvent>(disabledMethods);
        var playerClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var enemyClanId = TestEnvironment.CreateRegisteredObject<Clan>();

        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MapEvent>(battle.MapEventId, out var mapEvent));
                Assert.True(instance.ObjectManager.TryGetObject<SiegeEvent>(siegeId, out var siege));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(battle.AttackerPartyId, out var attacker));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(battle.DefenderPartyId, out var defender));
                Assert.True(instance.ObjectManager.TryGetObject<Clan>(playerClanId, out var playerClan));
                Assert.True(instance.ObjectManager.TryGetObject<Clan>(enemyClanId, out var enemyClan));

                using (new AllowedThread())
                {
                    party.ActualClan = playerClan;
                    defender.ActualClan = playerClan;
                    attacker.ActualClan = enemyClan;
                }

                StageSettlementSiege(mapEvent, siege, party);
            }, disabledMethods);
        }

        // Vanilla's Init needs a whole town and its menu models; PromptSiegeDefense still starts the encounter.
        var withoutEncounterInit = disabledMethods
            .Append(AccessTools.Method(typeof(PlayerEncounter), nameof(PlayerEncounter.Init),
                new[] { typeof(PartyBase), typeof(PartyBase), typeof(Settlement) }))
            .ToList();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(battle.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(battle.AttackerPartyId, out var attacker));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            using (new AllowedThread())
                FactionManager.DeclareWar(attacker.MapFaction, party.MapFaction);
            Assert.True(mapEvent.CanPartyJoinBattle(party.Party, BattleSideEnum.Defender));

            Server.Resolve<IMessageBroker>().Publish(this, new SiegeAssaultStarted(attacker, mapEvent.MapEventSettlement));

            Assert.Same(mapEvent.DefenderSide, party.Party.MapEventSide);
            Assert.Same(mapEvent.MapEventSettlement, party.CurrentSettlement);
        }, withoutEncounterInit);
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkPromptSiegeDefense>());

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(battle.MapEventId, out var mapEvent));
            Assert.Same(mapEvent.DefenderSide, PartyBase.MainParty.MapEventSide);
            Assert.NotNull(PlayerEncounter.Current);

            var behavior = ObjectHelper.SkipConstructor<EncounterGameMenuBehavior>();
            Assert.False(behavior.game_menu_encounter_leave_on_condition(new MenuCallbackArgs((MenuContext)null, null)));
        }, MapEventDisabledMethods);
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
                Server.Resolve<IBattleMissionInitializerResolver>(),
                null!, Server.Resolve<IBattleHostRetentionPolicy>());

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
                Server.Resolve<IBattleMissionInitializerResolver>(),
                null!, Server.Resolve<IBattleHostRetentionPolicy>());

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
                mapEvent.SetBattleType(battleType);
            });
        }
    }

    private string SetClientOnlyCamp(EnvironmentInstance client, string partyId)
    {
        var disabledMethods = SiegeLeaveDisabledMethods
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

    private static void StageSettlementSiege(MapEvent mapEvent, SiegeEvent siege, MobileParty party)
    {
        mapEvent.SetBattleType(MapEvent.BattleTypes.Siege);
        var settlement = siege.BesiegedSettlement;
        settlement.SiegeEvent = siege;
        settlement.Party._mapEventSide = mapEvent.DefenderSide;
        mapEvent.DefenderSide.LeaderParty = settlement.Party;
        mapEvent.SetMapEventSettlement(settlement);
        party._currentSettlement = settlement;
    }

    private static void SetMainParty(EnvironmentInstance instance, string partyId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Campaign.Current.MainParty = party;
        });
    }

    private List<MethodBase> LeaveSoldiersBehindDisabledMethods => MapEventDisabledMethods
        .Concat(SiegeLeaveDisabledMethods)
        .Append(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ActivateGameMenu), new[] { typeof(string) }))
        .Append(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.SwitchToMenu), new[] { typeof(string) }))
        .Append(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ExitToLast)))
        .ToList();

    private string SetMainHero(EnvironmentInstance instance)
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Game.Current.PlayerTroop = hero.CharacterObject;
            Assert.Same(hero, Hero.MainHero);
        });
        return heroId;
    }

    private static void AddEncounterGameMenuBehavior(EnvironmentInstance instance)
    {
        instance.Call(() =>
        {
            if (Campaign.Current.GetCampaignBehavior<EncounterGameMenuBehavior>() != null) return;

            Campaign.Current.AddCampaignBehaviorManager(new CampaignBehaviorManager(
                new CampaignBehaviorBase[] { new EncounterGameMenuBehavior() }));
        });
    }

    private static void InvokePatchedLeaveSoldiersBehindAccept()
    {
        var method = AccessTools.Method(typeof(EncounterGameMenuBehavior), nameof(EncounterGameMenuBehavior.game_menu_encounter_leave_your_soldiers_behind_accept_on_consequence));
        Assert.NotNull(method);

        var behavior = ObjectHelper.SkipConstructor<EncounterGameMenuBehavior>();
        method.Invoke(behavior, new object[] { null! });
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

    /// <summary>Records menu ids passed to <see cref="GameMenu.ActivateGameMenu(string)"/>.</summary>
    private static MethodCallRecorder CreateMenuActivationRecorder() => new MethodCallRecorder(Priority.Last,
        AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ActivateGameMenu), new[] { typeof(string) }));

    /// <summary>Records menu ids passed to <see cref="GameMenu.SwitchToMenu(string)"/>.</summary>
    private static MethodCallRecorder CreateMenuSwitchRecorder() => new MethodCallRecorder(Priority.First,
        AccessTools.Method(typeof(GameMenu), nameof(GameMenu.SwitchToMenu), new[] { typeof(string) }));

    /// <summary>Pushes a map state showing <paramref name="menuId"/>; must run inside the client's game call.</summary>
    private static void OpenClientMenu(string menuId)
    {
        var mapState = Game.Current.GameStateManager.CreateState<MapState>();
        mapState._menuContext = ObjectHelper.SkipConstructor<MenuContext>();
        mapState._menuContext.GameMenu = new GameMenu(menuId);
        Game.Current.GameStateManager._gameStates.Add(mapState);
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

    // Records each server sacrifice and whether the party was still in its map event. The body is skipped
    // because it needs live campaign models.
    private sealed class LeftSoldiersBehindRecorder : IDisposable
    {
        private static readonly MethodInfo ConsequenceMethod = AccessTools.Method(
            typeof(EncounterGameMenuBehaviorInterface),
            nameof(EncounterGameMenuBehaviorInterface.LeftSoldiersBehindConsequence));

        private static List<bool> inMapEvent = new();

        private readonly Harmony harmony = new($"leave-soldiers-behind-recorder-{Guid.NewGuid()}");

        public IReadOnlyList<bool> InMapEvent => inMapEvent;

        public LeftSoldiersBehindRecorder()
        {
            inMapEvent = new List<bool>();
            harmony.Patch(ConsequenceMethod, prefix: new HarmonyMethod(typeof(LeftSoldiersBehindRecorder), nameof(Record)));
        }

        public void Dispose()
        {
            harmony.Unpatch(ConsequenceMethod, HarmonyPatchType.Prefix, harmony.Id);
        }

        private static bool Record(MobileParty mainParty)
        {
            inMapEvent.Add(mainParty.MapEvent != null);
            return false;
        }
    }

    private sealed class NetworkDeliveryBlocker : IDisposable
    {
        private static readonly MethodInfo DeliveryMethod = AccessTools.Method(
            typeof(E2E.Tests.Environment.TestNetworkRouter),
            nameof(E2E.Tests.Environment.TestNetworkRouter.SendReliablePayload),
            new[] { typeof(LiteNetLib.NetPeer), typeof(LiteNetLib.NetPeer), typeof(byte[]) });

        private readonly Harmony harmony = new($"siege-join-network-blocker-{Guid.NewGuid()}");

        public NetworkDeliveryBlocker()
        {
            var prefix = new HarmonyMethod(typeof(NetworkDeliveryBlocker), nameof(Block));
            harmony.Patch(DeliveryMethod, prefix: prefix);
        }

        public void Dispose()
        {
            harmony.Unpatch(DeliveryMethod, HarmonyPatchType.Prefix, harmony.Id);
        }

        private static bool Block(ref byte[] payload)
        {
            if (!GameInterface.ContainerProvider.TryResolve<ICommonSerializer>(out var serializer))
                return true;

            object received = serializer.Deserialize(payload);
            if (received is IMessage message)
                return ShouldDeliver(message);
            if (received is not AggregateMessagePacket aggregate || aggregate.Messages == null)
                return true;

            byte[][] deliverable = aggregate.Messages
                .Where(inner => ShouldDeliver(serializer.Deserialize<IMessage>(inner)))
                .ToArray();
            if (deliverable.Length == 0)
                return false;

            payload = deliverable.Length == 1
                ? deliverable[0]
                : serializer.Serialize(new AggregateMessagePacket(deliverable));
            return true;
        }

        private static bool ShouldDeliver(IMessage message) =>
            message is not NetworkAddInvolvedParties &&
            message is not NetworkJoinBattleReply;
    }
}
