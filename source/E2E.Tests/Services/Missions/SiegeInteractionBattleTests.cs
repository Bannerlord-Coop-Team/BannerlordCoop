using Autofac;
using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Extensions;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Logging;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.MapEvents.TroopSupply.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.SiegeEvents.Patches;
using HarmonyLib;
using Moq;
using SandBox;
using SandBox.GauntletUI.Map;
using System.Collections.Concurrent;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
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

    [Theory]
    [InlineData(MapEvent.BattleTypes.Siege, false)]
    [InlineData(MapEvent.BattleTypes.SallyOut, false)]
    [InlineData(MapEvent.BattleTypes.SiegeOutside, false)]
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
        Server.Call(() =>
        {
            var handler = Server.Resolve<BattleMissionStartHandler>();
            var snapshotsField = AccessTools.Field(typeof(BattleMissionStartHandler),
                "siegeMissionSnapshots");
            Assert.NotNull(snapshotsField);
            var snapshots = Assert.IsType<ConcurrentDictionary<string, NetworkStartSiegeMission>>(
                snapshotsField.GetValue(handler));
            snapshots[battle.MapEventId] = new NetworkStartSiegeMission(
                battle.MapEventId,
                2,
                new[] { 1f, 1f, 1f },
                Array.Empty<SiegeEngineState>(),
                Array.Empty<SiegeEngineState>(),
                initiatingPartyId: null,
                isSallyOut: true);
        });

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

    [Theory]
    [InlineData(BattleSideEnum.Defender, true, false)]
    [InlineData(BattleSideEnum.Attacker, false, true)]
    public void SiegeAmbush_ReturnToCastleResultIsDefenderVictory(
        BattleSideEnum playerSide, bool playerVictory, bool playerDefeated)
    {
        var result = SiegeMissionEndPatches.CreateSiegeAmbushResult(playerSide);

        Assert.Equal(BattleState.DefenderVictory, result.BattleState);
        Assert.Equal(playerVictory, result.PlayerVictory);
        Assert.Equal(playerDefeated, result.PlayerDefeated);
        Assert.True(result.BattleResolved);
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
            Campaign.Current.MainParty = mainParty;
            mapEvent.MapEventSettlement = CreateSettlementWithCenterScenes();
            var attackerEngineState = new SiegeEngineState("e2e_attacker_engine", 1, 50f, 100f);
            var defenderEngineState = new SiegeEngineState("e2e_defender_engine", 2, 75f, 125f);
            var snapshot = new NetworkStartSiegeMission(
                battle.MapEventId,
                2,
                new[] { 1f, 0.5f, 0.25f },
                new[] { attackerEngineState },
                new[] { defenderEngineState },
                battle.AttackerPlayerPartyId,
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
                client.Resolve<IBattleMissionInitializerResolver>());

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
                    isSallyOut: isSiegeAmbush));
                GameThread.Instance.Update(TimeSpan.FromMilliseconds(16));

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

    [Theory]
    [InlineData(MapEvent.BattleTypes.Siege, false)]
    [InlineData(MapEvent.BattleTypes.SallyOut, false)]
    [InlineData(MapEvent.BattleTypes.SiegeOutside, false)]
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
        Assert.DoesNotContain(nonHostReserves.SelectMany(message => message.Parties),
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
                resolver);

            try
            {
                ContainerProvider.SetContainer(scope);
                broker.Publish(this, new NetworkStartAttackMission(
                    battle.MapEventId,
                    1234,
                    default,
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
