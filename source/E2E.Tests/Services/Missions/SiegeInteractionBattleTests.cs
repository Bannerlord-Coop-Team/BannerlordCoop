using Autofac;
using Common;
using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using Common.Util;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Extensions;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Logging;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.MapEvents.TroopSupply.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.SiegeEvents.Patches;
using HarmonyLib;
using Moq;
using Missions.Battles;
using Missions.Messages;
using SandBox;
using SandBox.GauntletUI.Map;
using System.Collections.Concurrent;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// Full battle-stack contracts for every supported siege-related map interaction.
/// </summary>
public class SiegeInteractionBattleTests : MissionTestEnvironment
{
    private const int AttackerPlayerTroops = 7;
    private const int DefenderPlayerTroops = 5;
    private const int AttackerAiTroops = 3;
    private const int DefenderAiTroops = 4;

    public SiegeInteractionBattleTests(ITestOutputHelper output) : base(output, numClients: 3)
    {
    }

    [Theory]
    [InlineData(MapEvent.BattleTypes.Siege, false)]
    [InlineData(MapEvent.BattleTypes.SallyOut, false)]
    [InlineData(MapEvent.BattleTypes.SiegeOutside, false)]
    [InlineData(MapEvent.BattleTypes.None, true)]
    public void Interaction_SynchronizesIdentitySidesAndExactTroopCounts(
        MapEvent.BattleTypes eventType,
        bool isSiegeAmbush)
    {
        var battle = SetupInteraction(eventType, isSiegeAmbush, includeAiParties: true);

        AssertInteraction(Server, battle, eventType, isSiegeAmbush);
        foreach (var client in Clients)
            AssertInteraction(client, battle, eventType, isSiegeAmbush);
    }

    // The reserve builder only branches on siege ambush, so one non-ambush siege type is enough.
    [Theory]
    [InlineData(MapEvent.BattleTypes.Siege, false)]
    [InlineData(MapEvent.BattleTypes.None, true)]
    public void Interaction_ReservesCountEveryEligibleTroopOnceAndPreserveOwnership(
        MapEvent.BattleTypes eventType,
        bool isSiegeAmbush)
    {
        var battle = SetupInteraction(eventType, isSiegeAmbush, includeAiParties: true);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(battle.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(battle.AttackerPlayerPartyId, out var attackerParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(battle.DefenderPlayerPartyId, out var defenderParty));
            var attackerMapEventParty = mapEvent.FindMapEventParty(attackerParty.Party);
            var defenderMapEventParty = mapEvent.FindMapEventParty(defenderParty.Party);
            Assert.NotNull(attackerMapEventParty);
            Assert.NotNull(defenderMapEventParty);
            attackerMapEventParty!.OnTroopRouted(attackerMapEventParty.Troops.First().Descriptor);
            defenderMapEventParty!.OnTroopKilled(defenderMapEventParty.Troops.First().Descriptor);

            var builder = Server.Resolve<IBattleTroopReserveBuilder>();
            var present = new[] { "attacker", "defender" };

            var hostReserves = builder.GetOwnedReserves(mapEvent, "attacker", isHost: true,
                presentControllers: present);
            var defenderReserves = builder.GetOwnedReserves(mapEvent, "defender", isHost: false,
                presentControllers: present);

            Assert.Equal(AttackerPlayerTroops - 1 + AttackerAiTroops + DefenderAiTroops,
                CountReserveTroops(hostReserves));
            Assert.Equal(DefenderPlayerTroops - 1, CountReserveTroops(defenderReserves));
            Assert.Equal(AttackerPlayerTroops - 1 + AttackerAiTroops,
                CountReserveTroops(hostReserves, BattleSideEnum.Attacker));
            Assert.Equal(DefenderAiTroops,
                CountReserveTroops(hostReserves, BattleSideEnum.Defender));
            Assert.Equal(DefenderPlayerTroops - 1,
                CountReserveTroops(defenderReserves, BattleSideEnum.Defender));
        });
    }

    [Fact]
    public void SiegeAmbush_DefenderSupplyOrderIsContiguousAcrossOwners()
    {
        var battle = SetupInteraction(MapEvent.BattleTypes.None, isSiegeAmbush: true,
            includeAiParties: true);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(battle.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(battle.AttackerPlayerPartyId,
                out var initiatingParty));
            var builder = Server.Resolve<IBattleTroopReserveBuilder>();
            builder.PrepareMissionReserves(mapEvent, initiatingParty);

            var present = new[] { "attacker", "defender" };
            var hostReserves = builder.GetOwnedReserves(mapEvent, "attacker", isHost: true,
                presentControllers: present);
            var defenderReserves = builder.GetOwnedReserves(mapEvent, "defender", isHost: false,
                presentControllers: present);
            var defenderOrders = hostReserves.Concat(defenderReserves)
                .Where(reserve => reserve.Side == BattleSideEnum.Defender)
                .SelectMany(reserve => reserve.Parties)
                .SelectMany(party => party.Entries)
                .Select(entry => entry.SupplyOrder)
                .OrderBy(order => order)
                .ToArray();

            Assert.Equal(Enumerable.Range(1, DefenderPlayerTroops + DefenderAiTroops),
                defenderOrders);
            Assert.All(hostReserves
                    .Where(reserve => reserve.Side == BattleSideEnum.Attacker)
                    .SelectMany(reserve => reserve.Parties)
                    .SelectMany(party => party.Entries),
                entry => Assert.Equal(0, entry.SupplyOrder));
        });
    }

    [Theory]
    [InlineData(MapEvent.BattleTypes.SallyOut)]
    [InlineData(MapEvent.BattleTypes.SiegeOutside)]
    public void Interaction_StartRequestBroadcastsOneMissionStartPerParticipantOnly(
        MapEvent.BattleTypes eventType)
    {
        var battle = SetupInteraction(eventType, isSiegeAmbush: false, includeAiParties: false);
        var clients = Clients.ToArray();
        var attackerClient = clients[0];
        Server.NetworkSentMessages.Clear();
        foreach (var client in clients)
            client.InternalMessages.Clear();

        attackerClient.Call(() => attackerClient.Resolve<INetwork>().SendAll(
            new NetworkBattleStartRequest(
                Guid.NewGuid().ToString(),
                (int)BattleStartMode.Mission,
                battle.MapEventId,
                battle.AttackerPlayerPartyId)), MapEventDisabledMethods);

        var starts = Server.NetworkSentMessages.GetMessages<NetworkStartAttackMission>().ToArray();
        Assert.Equal(2, starts.Length);
        Assert.All(starts, start => Assert.Equal(battle.MapEventId, start.MapEventId));
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkStartSiegeMission>());
        Assert.Single(clients[0].InternalMessages.GetMessages<NetworkStartAttackMission>(),
            start => start.MapEventId == battle.MapEventId);
        Assert.Single(clients[1].InternalMessages.GetMessages<NetworkStartAttackMission>(),
            start => start.MapEventId == battle.MapEventId);
        Assert.DoesNotContain(clients[2].InternalMessages.GetMessages<NetworkStartAttackMission>(),
            start => start.MapEventId == battle.MapEventId);
    }

    [Fact]
    public void SiegeAmbush_StartRequestBroadcastsSiegeMissionToParticipants()
    {
        var battle = SetupInteraction(MapEvent.BattleTypes.None, isSiegeAmbush: true,
            includeAiParties: false);
        SeedSiegeMissionSnapshot(battle.MapEventId, isSallyOut: true);

        var clients = Clients.ToArray();
        var attackerClient = clients[0];
        Server.NetworkSentMessages.Clear();
        foreach (var client in clients)
            client.InternalMessages.Clear();

        attackerClient.Call(() => attackerClient.Resolve<INetwork>().SendAll(
            new NetworkBattleStartRequest(
                Guid.NewGuid().ToString(),
                (int)BattleStartMode.Mission,
                battle.MapEventId,
                battle.AttackerPlayerPartyId)), MapEventDisabledMethods);

        var starts = Server.NetworkSentMessages.GetMessages<NetworkStartSiegeMission>().ToArray();
        Assert.Equal(2, starts.Length);
        Assert.All(starts, start =>
        {
            Assert.Equal(battle.MapEventId, start.MapEventId);
            Assert.Equal("e2e-siege-settlement", start.SettlementId);
            Assert.True(start.IsSallyOut);
        });
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkStartAttackMission>());
        Assert.Single(clients[0].InternalMessages.GetMessages<NetworkStartSiegeMission>(),
            start => start.MapEventId == battle.MapEventId);
        Assert.Single(clients[1].InternalMessages.GetMessages<NetworkStartSiegeMission>(),
            start => start.MapEventId == battle.MapEventId);
        Assert.DoesNotContain(clients[2].InternalMessages.GetMessages<NetworkStartSiegeMission>(),
            start => start.MapEventId == battle.MapEventId);
    }

    // Relief parties joining or leaving the defenders flip a live battle between Siege and SiegeOutside.
    [Theory]
    [InlineData(MapEvent.BattleTypes.SiegeOutside, true)]
    [InlineData(MapEvent.BattleTypes.Siege, false)]
    public void FlippedSiegeType_StartRequestReusesOpenMissionKind(
        MapEvent.BattleTypes liveType,
        bool openedAsSiege)
    {
        var battle = SetupInteraction(liveType, isSiegeAmbush: false, includeAiParties: false);
        if (openedAsSiege)
            SeedSiegeMissionSnapshot(battle.MapEventId, isSallyOut: false);
        else
            Server.Call(() => Server.Resolve<BattleMissionStartHandler>().GetOrCreateMissionInitializerSnapshot(
                battle.MapEventId, () => new MissionInitializerRecord("e2e_land_battle")));
        Server.NetworkSentMessages.Clear();

        var attackerClient = Clients.First();
        attackerClient.Call(() => attackerClient.Resolve<INetwork>().SendAll(
            new NetworkBattleStartRequest(
                Guid.NewGuid().ToString(),
                (int)BattleStartMode.Mission,
                battle.MapEventId,
                battle.AttackerPlayerPartyId)), MapEventDisabledMethods);

        var siegeStarts = Server.NetworkSentMessages.GetMessages<NetworkStartSiegeMission>().ToArray();
        var attackStarts = Server.NetworkSentMessages.GetMessages<NetworkStartAttackMission>().ToArray();
        Assert.Equal(openedAsSiege ? 2 : 0, siegeStarts.Length);
        Assert.Equal(openedAsSiege ? 0 : 2, attackStarts.Length);
        Assert.All(attackStarts, start => Assert.Equal("e2e_land_battle", start.MissionInitializer.SceneName));
    }

    // Joiners only share a snapshot while its mission is open, so claim the mission too, otherwise the next
    // start request is a new claim and rebuilds the snapshot from the campaign.
    private void SeedSiegeMissionSnapshot(string mapEventId, bool isSallyOut)
    {
        Server.Call(() =>
        {
            Assert.True(ServerBattleModeArbiter.TryClaimMission(mapEventId));
            var handler = Server.Resolve<BattleMissionStartHandler>();
            var snapshotsField = AccessTools.Field(typeof(BattleMissionStartHandler),
                "siegeMissionSnapshots");
            Assert.NotNull(snapshotsField);
            var snapshots = Assert.IsType<ConcurrentDictionary<string, NetworkStartSiegeMission>>(
                snapshotsField.GetValue(handler));
            snapshots[mapEventId] = new NetworkStartSiegeMission(
                mapEventId,
                2,
                new[] { 1f, 1f, 1f },
                Array.Empty<SiegeEngineState>(),
                Array.Empty<SiegeEngineState>(),
                initiatingPartyId: null,
                settlementId: "e2e-siege-settlement",
                isSallyOut: isSallyOut);
        });
    }

    // Issue #1842: when the assault mission empties the siege map event lives on, so the next assault on it
    // must load the engine damage the last assault wrote back, not that assault's cached snapshot. Relief
    // parties can flip the live type to SiegeOutside while it is empty, and the reassault still opens as a siege.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SiegeReassault_AfterInstanceEmptied_LoadsEngineDamageFromLastAssault(bool flipsToSiegeOutside)
    {
        const string engineTypeId = "issue_1842_test_catapult";
        var battle = SetupInteraction(MapEvent.BattleTypes.Siege, isSiegeAmbush: false, includeAiParties: false);
        foreach (var instance in Clients.Append(Server))
            instance.Call(() => EnsureRangedEngineType(engineTypeId));

        SiegeEvent.SiegeEngineConstructionProgress? engine = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(battle.MapEventId, out var mapEvent));
            engine = SeedBesiegerCampEngine(mapEvent, EnsureRangedEngineType(engineTypeId));
        });

        var firstStarts = RequestSiegeMission(battle);
        Assert.All(firstStarts, start => Assert.Equal(100f, Assert.Single(start.AttackerEngines).Health));

        // The mission host reports the damaged engine as the assault ends, then the last player leaves.
        var attackerClient = Clients.First();
        attackerClient.Call(() => attackerClient.Resolve<INetwork>().SendAll(new NetworkSiegeEngineStatesReport(
            battle.MapEventId,
            new[] { new SiegeEngineState(engineTypeId, 0, 40f, 100f) },
            Array.Empty<SiegeEngineState>())), MapEventDisabledMethods);
        Server.Call(() => Assert.Equal(40f, engine!.Hitpoints));
        DepartBattle("attacker", battle.MapEventId, wasRetreat: true, isInstanceEmpty: true);
        if (flipsToSiegeOutside)
        {
            foreach (var instance in Clients.Append(Server))
            {
                instance.Call(() =>
                {
                    Assert.True(instance.ObjectManager.TryGetObject<MapEvent>(battle.MapEventId, out var mapEvent));
                    mapEvent._mapEventType = MapEvent.BattleTypes.SiegeOutside;
                });
            }
        }

        var secondStarts = RequestSiegeMission(battle);
        Assert.All(secondStarts, start =>
        {
            Assert.Equal(40f, Assert.Single(start.AttackerEngines).Health);
            Assert.False(start.IsSallyOut);
        });
        var clients = Clients.ToArray();
        foreach (var client in clients.Take(2))
        {
            var start = Assert.Single(client.InternalMessages.GetMessages<NetworkStartSiegeMission>());
            Assert.Equal(40f, Assert.Single(start.AttackerEngines).Health);
        }
        Assert.Empty(clients[2].InternalMessages.GetMessages<NetworkStartSiegeMission>());
        Assert.Empty(clients[2].InternalMessages.GetMessages<NetworkStartAttackMission>());
    }

    private NetworkStartSiegeMission[] RequestSiegeMission(InteractionContext battle)
    {
        Server.NetworkSentMessages.Clear();
        foreach (var client in Clients)
            client.InternalMessages.Clear();

        var attackerClient = Clients.First();
        attackerClient.Call(() => attackerClient.Resolve<INetwork>().SendAll(
            new NetworkBattleStartRequest(
                Guid.NewGuid().ToString(),
                (int)BattleStartMode.Mission,
                battle.MapEventId,
                battle.AttackerPlayerPartyId)), MapEventDisabledMethods);

        var starts = Server.NetworkSentMessages.GetMessages<NetworkStartSiegeMission>().ToArray();
        Assert.Equal(2, starts.Length);
        return starts;
    }

    private static SiegeEngineType EnsureRangedEngineType(string engineTypeId)
    {
        var engineType = MBObjectManager.Instance.GetObject<SiegeEngineType>(engineTypeId);
        if (engineType != null) return engineType;

        engineType = ObjectHelper.SkipConstructor<SiegeEngineType>();
        using (new AllowedThread()) engineType.StringId = engineTypeId;
        engineType.IsRanged = true;
        MBObjectManager.Instance.RegisterObject(engineType);
        return engineType;
    }

    // Vanilla's siege constructors need a full campaign, so wire the minimal graph the mission snapshot and the
    // engine write-back read. The engine itself is built with patches live so it registers like a real one.
    private static SiegeEvent.SiegeEngineConstructionProgress SeedBesiegerCampEngine(
        MapEvent mapEvent,
        SiegeEngineType engineType)
    {
        var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
        var siegeEvent = ObjectHelper.SkipConstructor<SiegeEvent>();
        var besiegerCamp = ObjectHelper.SkipConstructor<BesiegerCamp>();
        var engine = new SiegeEvent.SiegeEngineConstructionProgress(engineType, 1f, 100f);

        using (new AllowedThread())
        {
            besiegerCamp.SiegeEngines = new SiegeEvent.SiegeEnginesContainer(BattleSideEnum.Attacker, null);
            besiegerCamp.SiegeEngines._deployedSiegeEngines.Add(engine);
            AccessTools.Field(typeof(SiegeEvent), nameof(SiegeEvent.BesiegerCamp)).SetValue(siegeEvent, besiegerCamp);
            AccessTools.Field(typeof(SiegeEvent), nameof(SiegeEvent.BesiegedSettlement)).SetValue(siegeEvent, settlement);
            settlement.SiegeEngines = new SiegeEvent.SiegeEnginesContainer(BattleSideEnum.Defender, null);
            settlement.Town = ObjectHelper.SkipConstructor<Town>();
            settlement.Town.Buildings = new MBList<Building>();
            settlement.SiegeEvent = siegeEvent;
            mapEvent.MapEventSettlement = settlement;
        }

        return engine;
    }

    [Fact]
    public void SiegeAmbush_ReportsSurvivingEngineStates()
    {
        var battle = SetupInteraction(MapEvent.BattleTypes.None, isSiegeAmbush: true,
            includeAiParties: false);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(battle.MapEventId, out var mapEvent));
            Assert.True(SiegeEngineStateReporter.ShouldReport(mapEvent));
        });
    }

    [Fact]
    public void SiegeAmbush_AllMembersComplete_FinalizesWithoutVictoryState()
    {
        var battle = SetupInteraction(MapEvent.BattleTypes.None, isSiegeAmbush: true,
            includeAiParties: false);
        var participants = Clients.Take(2).ToArray();
        EnterBattle(participants[0], battle.MapEventId);
        EnterBattle(participants[1], battle.MapEventId);
        Server.SimulateMessage(participants[0].NetPeer,
            new NetworkMissionEntered("attacker", battle.MapEventId));
        Server.SimulateMessage(participants[1].NetPeer,
            new NetworkMissionEntered("defender", battle.MapEventId));
        BattleState? finalizedState = null;
        Server.Call(() => Server.Resolve<IMessageBroker>().Subscribe<MapEventFinalized>(
            payload => finalizedState = payload.What.MapEvent.BattleState));

        foreach (var participant in participants)
        {
            participant.Call(() =>
            {
                Assert.True(participant.Resolve<IBattleHostRegistry>().TryGet(
                    battle.MapEventId,
                    out var assignment));
                participant.Resolve<INetwork>().SendAll(new NetworkBattleResultReady(
                    battle.MapEventId,
                    BattleState.DefenderPullBack,
                    assignment.Epoch));
            }, MapEventDisabledMethods);
        }

        Assert.Equal(BattleState.None, finalizedState);
        Server.Call(() => Assert.False(
            Server.ObjectManager.TryGetObject<MapEvent>(battle.MapEventId, out _)));
    }

    [Fact]
    public void SiegeAmbush_EarlyDefenderPullBackApply_DoesNotBlockResultReadyCompletion()
    {
        var battle = SetupInteraction(MapEvent.BattleTypes.None, isSiegeAmbush: true,
            includeAiParties: false);
        var participants = Clients.Take(2).ToArray();
        EnterBattle(participants[0], battle.MapEventId);
        EnterBattle(participants[1], battle.MapEventId);
        Server.SimulateMessage(participants[0].NetPeer,
            new NetworkMissionEntered("attacker", battle.MapEventId));
        Server.SimulateMessage(participants[1].NetPeer,
            new NetworkMissionEntered("defender", battle.MapEventId));
        BattleState? finalizedState = null;
        Server.Call(() => Server.Resolve<IMessageBroker>().Subscribe<MapEventFinalized>(
            payload => finalizedState = payload.What.MapEvent.BattleState));

        participants[0].Call(() =>
        {
            Assert.True(participants[0].Resolve<IBattleHostRegistry>().TryGet(
                battle.MapEventId,
                out var assignment));
            var network = participants[0].Resolve<INetwork>();
            network.SendAll(new NetworkChangeBattleState(
                battle.MapEventId,
                BattleState.DefenderPullBack,
                assignment.Epoch));
            network.SendAll(new NetworkBattleResultReady(
                battle.MapEventId,
                BattleState.DefenderPullBack,
                assignment.Epoch));
        }, MapEventDisabledMethods);

        participants[1].Call(() =>
        {
            Assert.True(participants[1].Resolve<IBattleHostRegistry>().TryGet(
                battle.MapEventId,
                out var assignment));
            participants[1].Resolve<INetwork>().SendAll(new NetworkBattleResultReady(
                battle.MapEventId,
                BattleState.DefenderPullBack,
                assignment.Epoch));
        }, MapEventDisabledMethods);

        Assert.Equal(BattleState.None, finalizedState);
        Server.Call(() => Assert.False(
            Server.ObjectManager.TryGetObject<MapEvent>(battle.MapEventId, out _)));
    }

    [Fact]
    public void SiegeAmbush_MissionEndWaitsForAuthoritativeCompletion()
    {
        var battle = SetupInteraction(MapEvent.BattleTypes.None, isSiegeAmbush: true,
            includeAiParties: false);
        var client = Clients.First();

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(battle.MapEventId, out var mapEvent));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(battle.DefenderPlayerPartyId, out var mainParty));
            var previousMainParty = Campaign.Current.MainParty;
            var previousEncounter = Campaign.Current.PlayerEncounter;
            bool previousConclusionGate = BattleConclusionGate.IsInCoopBattleMission;

            try
            {
                Campaign.Current.MainParty = mainParty;
                PlayerEncounter.Start();
                PlayerEncounter.Current._mapEvent = mapEvent;
                PlayerEncounter.Current.SetIsSallyOutAmbush(true);
                BattleConclusionGate.IsInCoopBattleMission = true;

                new SiegeAmbushCampaignBehavior().OnMissionEnded(Mock.Of<IMission>());

                Assert.Same(mapEvent, PlayerEncounter.Battle);
                Assert.False(mapEvent.IsFinalized);
                Assert.False(PlayerEncounter.Current._isSallyOutAmbush);
            }
            finally
            {
                BattleConclusionGate.IsInCoopBattleMission = previousConclusionGate;
                Campaign.Current.MainParty = previousMainParty;
                Campaign.Current.PlayerEncounter = previousEncounter;
            }
        });
    }

    [Theory]
    [InlineData(MapEvent.BattleTypes.Siege, false)]
    [InlineData(MapEvent.BattleTypes.None, true)]
    public void WallsInteraction_UsesSettlementCenterSceneAndPreservesSiegeSnapshot(
        MapEvent.BattleTypes eventType,
        bool isSiegeAmbush)
    {
        var battle = SetupInteraction(eventType, isSiegeAmbush, includeAiParties: false);
        var client = Clients.First();
        var launcher = new Mock<ICoopSiegeBattleLauncher>();
        MissionInitializerRecord? capturedRecord = null;
        float[]? capturedWallRatios = null;
        List<MissionSiegeWeapon>? capturedAttackerWeapons = null;
        List<MissionSiegeWeapon>? capturedDefenderWeapons = null;
        bool? capturedIsSallyOut = null;
        launcher.Setup(value => value.OpenCoopSiegeBattle(
                It.IsAny<MissionInitializerRecord>(),
                It.IsAny<float[]>(),
                It.IsAny<List<MissionSiegeWeapon>>(),
                It.IsAny<List<MissionSiegeWeapon>>(),
                It.IsAny<bool>()))
            .Callback<MissionInitializerRecord, float[], List<MissionSiegeWeapon>, List<MissionSiegeWeapon>, bool>(
                (record, wallRatios, attackerWeapons, defenderWeapons, isSallyOut) =>
                {
                    capturedRecord = record;
                    capturedWallRatios = wallRatios;
                    capturedAttackerWeapons = attackerWeapons;
                    capturedDefenderWeapons = defenderWeapons;
                    capturedIsSallyOut = isSallyOut;
                })
            .Returns((Mission)null!);

        using var scope = client.Container.BeginLifetimeScope(builder =>
            builder.RegisterInstance(launcher.Object).As<ICoopSiegeBattleLauncher>());

        var disabledMethods = MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(DefaultMapWeatherModel),
                nameof(DefaultMapWeatherModel.GetAtmosphereModel)))
            .ToList();
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(battle.MapEventId, out var mapEvent));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(battle.AttackerPlayerPartyId, out var mainParty));
            var previousMainParty = Campaign.Current.MainParty;
            var previousSettlement = mapEvent.MapEventSettlement;
            var previousMilitaryPowerModel = Campaign.Current.Models.MilitaryPowerModel;
            Campaign.Current.MainParty = mainParty;
            Campaign.Current.Models.MilitaryPowerModel = Mock.Of<MilitaryPowerModel>();
            var missionSettlement = CreateSettlementWithCenterScenes();
            if (!client.ObjectManager.TryGetId(missionSettlement, out var missionSettlementId))
                Assert.True(client.ObjectManager.AddNewObject(missionSettlement, out missionSettlementId));
            mapEvent.MapEventSettlement = null;
            var attackerEngineState = new SiegeEngineState("e2e_attacker_engine", 1, 50f, 100f);
            var defenderEngineState = new SiegeEngineState("e2e_defender_engine", 2, 75f, 125f);
            var snapshot = new NetworkStartSiegeMission(
                battle.MapEventId,
                2,
                new[] { 1f, 0.5f, 0.25f },
                new[] { attackerEngineState },
                new[] { defenderEngineState },
                battle.AttackerPlayerPartyId,
                settlementId: missionSettlementId,
                isSallyOut: isSiegeAmbush);
            Assert.Same(attackerEngineState, Assert.Single(snapshot.AttackerEngines));
            Assert.Same(defenderEngineState, Assert.Single(snapshot.DefenderEngines));
            Assert.Equal(isSiegeAmbush, snapshot.IsSallyOut);

            using var broker = new MessageBroker();
            using var handler = new BattleMissionStartHandler(
                broker,
                client.ObjectManager,
                client.Resolve<IPlayerManager>(),
                client.Resolve<INetwork>(),
                client.Resolve<IMapEventLogger>(),
                client.Resolve<IBattleMissionInitializerResolver>(),
                null!, Mock.Of<IBattleHostRetentionPolicy>());

            try
            {
                ContainerProvider.SetContainer(scope);
                broker.Publish(this, new NetworkStartSiegeMission(
                    battle.MapEventId,
                    2,
                    new[] { 1f, 0.5f, 0.25f },
                    Array.Empty<SiegeEngineState>(),
                    Array.Empty<SiegeEngineState>(),
                    battle.AttackerPlayerPartyId,
                    settlementId: missionSettlementId,
                    isSallyOut: isSiegeAmbush));
                GameThread.Instance.Update(TimeSpan.FromMilliseconds(16));

                Assert.Same(missionSettlement, mapEvent.MapEventSettlement);
                Assert.NotNull(capturedRecord);
                Assert.Equal("e2e_siege_level_2", capturedRecord!.Value.SceneName);
                Assert.Equal(new[] { 1f, 0.5f, 0.25f }, capturedWallRatios);
                Assert.Empty(capturedAttackerWeapons!);
                Assert.Empty(capturedDefenderWeapons!);
                Assert.Equal(isSiegeAmbush, capturedIsSallyOut);
            }
            finally
            {
                Campaign.Current.MainParty = previousMainParty;
                Campaign.Current.Models.MilitaryPowerModel = previousMilitaryPowerModel;
                mapEvent.MapEventSettlement = previousSettlement;
                ContainerProvider.SetContainer(client.Container);
                BattleSpawnGate.EndBattle();
            }
        }, disabledMethods);
    }

    [Theory]
    [InlineData(MapEvent.BattleTypes.SallyOut)]
    [InlineData(MapEvent.BattleTypes.SiegeOutside)]
    public void LandOutsideInteraction_UsesFieldBattleLauncher(MapEvent.BattleTypes eventType)
    {
        AssertAttackMissionLauncher(eventType, expectedScene: "e2e_land_battle");
    }

    // The reserve builder only branches on siege ambush, so one non-ambush siege type is enough.
    [Theory]
    [InlineData(MapEvent.BattleTypes.Siege, false)]
    [InlineData(MapEvent.BattleTypes.None, true)]
    public void Interaction_LateAiPartiesExpandHostReservesExactlyOnce(
        MapEvent.BattleTypes eventType,
        bool isSiegeAmbush)
    {
        var battle = SetupInteraction(eventType, isSiegeAmbush, includeAiParties: false);
        var clients = Clients.ToArray();
        var host = clients[0];
        var nonHost = clients[1];
        EnterBattle(host, battle.MapEventId);
        EnterBattle(nonHost, battle.MapEventId);
        host.InternalMessages.Clear();
        nonHost.InternalMessages.Clear();

        var attackerReinforcement = AddReinforcement(battle.MapEventId,
            BattleSideEnum.Attacker, AttackerAiTroops);
        var defenderReinforcement = AddReinforcement(battle.MapEventId,
            BattleSideEnum.Defender, DefenderAiTroops);

        Assert.Equal(2, host.InternalMessages.GetMessages<NetworkBattleReserveOwnershipExpanded>()
            .Count(message => message.MapEventId == battle.MapEventId));

        var hostReserves = LatestReserveSnapshot(host, battle.MapEventId);
        AssertReserveParty(hostReserves, attackerReinforcement, BattleSideEnum.Attacker,
            AttackerAiTroops);
        AssertReserveParty(hostReserves, defenderReinforcement, BattleSideEnum.Defender,
            DefenderAiTroops);
        Assert.Equal(1, hostReserves.SelectMany(message => message.Parties)
            .Count(party => party.PartyId == attackerReinforcement));
        Assert.Equal(1, hostReserves.SelectMany(message => message.Parties)
            .Count(party => party.PartyId == defenderReinforcement));

        var nonHostReserves = LatestReserveSnapshot(nonHost, battle.MapEventId);
        Assert.DoesNotContain(nonHostReserves.SelectMany(
                message => message.Parties ?? Array.Empty<PartyReserve>()),
            party => party.PartyId == attackerReinforcement || party.PartyId == defenderReinforcement);
    }

    private InteractionContext SetupInteraction(
        MapEvent.BattleTypes eventType,
        bool isSiegeAmbush,
        bool includeAiParties)
    {
        string? mapEventId = null;
        var partyIds = new string[includeAiParties ? 4 : 2];

        Server.Call(() =>
        {
            var parties = new MobileParty[partyIds.Length];
            for (var i = 0; i < parties.Length; i++)
                parties[i] = GameObjectCreator.CreateInitializedObject<MobileParty>();

            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
            mapEvent.MapEventVisual = ObjectHelper.SkipConstructor<GauntletMapEventVisual>();
            var fieldComponent = new FieldBattleEventComponent(mapEvent);

            // Non-field Initialize branches require a complete live siege/port graph. Build the synced graph
            // through the safe field path, then stamp the interaction identity on every replica below.
            mapEvent.Initialize(parties[0].Party, parties[1].Party, fieldComponent,
                MapEvent.BattleTypes.FieldBattle);

            if (includeAiParties)
            {
                parties[2].Party.MapEventSide = mapEvent.AttackerSide;
                parties[3].Party.MapEventSide = mapEvent.DefenderSide;
            }

            mapEvent.Component = isSiegeAmbush
                ? new SiegeAmbushEventComponent(mapEvent)
                : null;

            mapEvent.MapEventVisual = null;
            if (!Campaign.Current.MapEventManager.MapEvents.Contains(mapEvent))
                Campaign.Current.MapEventManager.OnMapEventCreated(mapEvent);

            Assert.True(Server.ObjectManager.TryGetId(mapEvent, out mapEventId));
            for (var i = 0; i < parties.Length; i++)
                Assert.True(Server.ObjectManager.TryGetId(parties[i], out partyIds[i]));
        }, MapEventDisabledMethods);

        Assert.NotNull(mapEventId);

        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MapEvent>(mapEventId!, out var mapEvent));
                mapEvent._mapEventType = eventType;
                mapEvent.Position = new CampaignVec2(default, isOnLand: true);
            });
        }

        var troopId = CreateRegisteredObject<CharacterObject>();
        SeedParty(partyIds[0], troopId, AttackerPlayerTroops);
        SeedParty(partyIds[1], troopId, DefenderPlayerTroops);
        if (includeAiParties)
        {
            SeedParty(partyIds[2], troopId, AttackerAiTroops);
            SeedParty(partyIds[3], troopId, DefenderAiTroops);
        }

        var attackerHeroId = CreateRegisteredObject<Hero>();
        var defenderHeroId = CreateRegisteredObject<Hero>();
        RegisterAsPlayerParty("attacker", attackerHeroId, partyIds[0]);
        RegisterAsPlayerParty("defender", defenderHeroId, partyIds[1]);

        var clients = Clients.ToArray();
        SetControllerId(clients[0], "attacker");
        SetControllerId(clients[1], "defender");
        ConnectRegisteredPlayer(clients[0], "attacker");
        ConnectRegisteredPlayer(clients[1], "defender");

        var outsiderHeroId = CreateRegisteredObject<Hero>();
        var outsiderPartyId = CreateRegisteredObject<MobileParty>();
        RegisterAsPlayerParty("outsider", outsiderHeroId, outsiderPartyId);
        SetControllerId(clients[2], "outsider");
        ConnectRegisteredPlayer(clients[2], "outsider");

        return new InteractionContext(
            mapEventId!,
            partyIds[0],
            partyIds[1],
            includeAiParties ? partyIds[2] : null,
            includeAiParties ? partyIds[3] : null);
    }

    private void SeedParty(string partyId, string characterId, int count)
    {
        void Seed(EnvironmentInstance instance)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(characterId, out var character));
                using (new AllowedThread())
                {
                    party.MemberRoster.Clear();
                    party.MemberRoster.AddNewElement(character, -1);
                    var index = party.MemberRoster.FindIndexOfTroop(character);
                    party.MemberRoster.AddToCountsAtIndex(index, count, 0, 0, removeDepleted: false);
                }

                var mapEventParty = party.MapEvent?.FindMapEventParty(party.Party);
                mapEventParty?.Update();
            });
        }

        Seed(Server);
        foreach (var client in Clients)
            Seed(client);
    }

    private static int CountReserveTroops(
        IReadOnlyList<SideReserve> reserves,
        BattleSideEnum? side = null)
    {
        return reserves
            .Where(reserve => side == null || reserve.Side == side)
            .SelectMany(reserve => reserve.Parties)
            .Sum(party => party.Entries.Length - party.SuppliedCount);
    }

    private static void AssertInteraction(
        EnvironmentInstance instance,
        InteractionContext context,
        MapEvent.BattleTypes eventType,
        bool isSiegeAmbush)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MapEvent>(context.MapEventId, out var mapEvent));
            Assert.Equal(eventType, mapEvent.EventType);
            Assert.Equal(isSiegeAmbush, mapEvent.IsSiegeAmbush);
            Assert.Equal(AttackerPlayerTroops + AttackerAiTroops, mapEvent.AttackerSide.TroopCount);
            Assert.Equal(DefenderPlayerTroops + DefenderAiTroops, mapEvent.DefenderSide.TroopCount);

            AssertPartyOnSide(instance, mapEvent, context.AttackerPlayerPartyId, BattleSideEnum.Attacker);
            AssertPartyOnSide(instance, mapEvent, context.DefenderPlayerPartyId, BattleSideEnum.Defender);
            AssertPartyOnSide(instance, mapEvent, context.AttackerAiPartyId!, BattleSideEnum.Attacker);
            AssertPartyOnSide(instance, mapEvent, context.DefenderAiPartyId!, BattleSideEnum.Defender);
        });
    }

    private static void AssertPartyOnSide(
        EnvironmentInstance instance,
        MapEvent mapEvent,
        string partyId,
        BattleSideEnum side)
    {
        Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
        Assert.Contains(mapEvent.GetMapEventSide(side).Parties, value => value.Party == party.Party);
    }

    private void AssertAttackMissionLauncher(
        MapEvent.BattleTypes eventType,
        string expectedScene)
    {
        var battle = SetupInteraction(eventType, isSiegeAmbush: false, includeAiParties: false);
        var client = Clients.First();
        var resolver = new RecordingMissionInitializerResolver(expectedScene);
        var fieldLauncher = new Mock<ICoopFieldBattleLauncher>();
        MissionInitializerRecord? captured = null;
        fieldLauncher.Setup(value => value.OpenCoopFieldBattle(It.IsAny<MissionInitializerRecord>()))
            .Callback<MissionInitializerRecord>(record => captured = record)
            .Returns((Mission)null!);

        using var scope = client.Container.BeginLifetimeScope(builder =>
            builder.RegisterInstance(fieldLauncher.Object).As<ICoopFieldBattleLauncher>());

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(battle.AttackerPlayerPartyId, out var mainParty));
            var previousMainParty = Campaign.Current.MainParty;
            Campaign.Current.MainParty = mainParty;
            PlayerEncounter.Start();
            PlayerEncounter.Current._mapEvent = mainParty.MapEvent;
            using var broker = new MessageBroker();
            using var handler = new BattleMissionStartHandler(
                broker,
                client.ObjectManager,
                client.Resolve<IPlayerManager>(),
                client.Resolve<INetwork>(),
                client.Resolve<IMapEventLogger>(),
                resolver,
                null!, Mock.Of<IBattleHostRetentionPolicy>());

            try
            {
                ContainerProvider.SetContainer(scope);
                broker.Publish(this, new NetworkStartAttackMission(
                    battle.MapEventId,
                    new MissionInitializerRecord(expectedScene) { RandomTerrainSeed = 1234 },
                    battle.AttackerPlayerPartyId));
                GameThread.Instance.Update(TimeSpan.FromMilliseconds(16));
                GameThread.Instance.Update(TimeSpan.FromMilliseconds(16));

                Assert.NotNull(captured);
                Assert.Equal(expectedScene, captured!.Value.SceneName);
            }
            finally
            {
                Campaign.Current.MainParty = previousMainParty;
                Campaign.Current.PlayerEncounter = null;
                ContainerProvider.SetContainer(client.Container);
                BattleSpawnGate.EndBattle();
            }
        });
    }

    private string AddReinforcement(
        string mapEventId,
        BattleSideEnum side,
        int count)
    {
        var partyId = CreateRegisteredObject<MobileParty>();
        var troopId = CreateRegisteredObject<CharacterObject>();
        SeedParty(partyId, troopId, count);
        string? mapEventPartyId = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            var interactionType = mapEvent._mapEventType;
            var interactionComponent = mapEvent.Component;
            mapEvent._mapEventType = MapEvent.BattleTypes.FieldBattle;
            mapEvent.Component = null;
            party.Party.MapEventSide = mapEvent.GetMapEventSide(side);
            mapEvent._mapEventType = interactionType;
            mapEvent.Component = interactionComponent;
            var mapEventParty = mapEvent.GetMapEventSide(side).Parties.Last(value => value.Party == party.Party);
            Assert.True(Server.ObjectManager.TryGetId(mapEventParty, out mapEventPartyId));
            Server.Resolve<IMessageBroker>().Publish(this,
                new MapEventInvolvedPartiesAdded(mapEvent, new[] { mapEventParty }));
        }, MapEventDisabledMethods);

        Assert.NotNull(mapEventPartyId);
        return mapEventPartyId!;
    }

    private static NetworkBattleTroopReserve[] LatestReserveSnapshot(
        EnvironmentInstance instance,
        string mapEventId)
    {
        var messages = instance.InternalMessages.GetMessages<NetworkBattleTroopReserve>()
            .Where(message => message.MapEventId == mapEventId)
            .ToArray();
        var revision = messages.Max(message => message.AllocationRevision);
        return messages.Where(message => message.AllocationRevision == revision).ToArray();
    }

    private static void AssertReserveParty(
        IEnumerable<NetworkBattleTroopReserve> reserves,
        string partyId,
        BattleSideEnum side,
        int expectedTroops)
    {
        var sideReserve = Assert.Single(reserves, reserve => reserve.Side == (int)side);
        var party = Assert.Single(sideReserve.Parties, reserve => reserve.PartyId == partyId);
        Assert.Equal(expectedTroops, party.Entries.Length - party.SuppliedCount);
    }

    private static Settlement CreateSettlementWithCenterScenes()
    {
        var complex = new LocationComplex();
        var center = new Location(
            "center",
            new TextObject("Center"),
            new TextObject("Center"),
            100,
            isIndoor: false,
            canBeReserved: false,
            "CanAlways",
            "CanAlways",
            "CanAlways",
            "CanAlways",
            new[]
            {
                "e2e_siege_level_0",
                "e2e_siege_level_1",
                "e2e_siege_level_2",
                "e2e_siege_level_3",
            },
            complex);
        complex._locations.Add("center", center);
        return new Settlement(new TextObject("E2E Siege Settlement"), complex, null);
    }

    private sealed class RecordingMissionInitializerResolver : IBattleMissionInitializerResolver
    {
        private readonly string sceneName;

        public RecordingMissionInitializerResolver(string sceneName)
        {
            this.sceneName = sceneName;
        }

        public MissionInitializerRecord Create(
            MapEvent mapEvent,
            int randomTerrainSeed,
            AtmosphereInfo atmosphereOnCampaign)
        {
            return new MissionInitializerRecord(sceneName)
            {
                RandomTerrainSeed = randomTerrainSeed,
                AtmosphereOnCampaign = atmosphereOnCampaign,
            };
        }
    }

    private sealed record InteractionContext(
        string MapEventId,
        string AttackerPlayerPartyId,
        string DefenderPlayerPartyId,
        string? AttackerAiPartyId,
        string? DefenderAiPartyId);
}

/// <summary>Siege ambush result construction; a static factory, so no game instances.</summary>
public class SiegeAmbushResultTests
{
    [Theory]
    [InlineData(BattleSideEnum.Defender, true, false)]
    [InlineData(BattleSideEnum.Attacker, false, true)]
    public void SiegeAmbush_ReturnToCastleResultUsesNoVictoryCompletionSignal(
        BattleSideEnum playerSide, bool playerVictory, bool playerDefeated)
    {
        var result = SiegeMissionEndPatches.CreateSiegeAmbushResult(playerSide);

        Assert.Equal(BattleState.DefenderPullBack, result.BattleState);
        Assert.Equal(playerVictory, result.PlayerVictory);
        Assert.Equal(playerDefeated, result.PlayerDefeated);
        Assert.True(result.BattleResolved);
    }
}
