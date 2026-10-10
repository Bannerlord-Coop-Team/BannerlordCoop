using Autofac;
using Common;
using Common.Messaging;
using Common.Network;
using GameInterface;
using GameInterface.Services.Hideouts;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Logging;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.Players;
using Moq;
using SandBox.GameComponents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

public class BattleMissionStartHandlerTests : MapEventTestBase
{
    public BattleMissionStartHandlerTests(ITestOutputHelper output) : base(output)
    {
    }

    [Theory]
    [InlineData(BattleSideEnum.Attacker, false, false, false)]
    [InlineData(BattleSideEnum.Defender, true, false, false)]
    [InlineData(BattleSideEnum.Attacker, false, true, true)]
    [InlineData(BattleSideEnum.Attacker, true, true, false)]
    public void AttackMissionStart_NonInitiatingClientInitializesJoinedPlayerEncounter(
        BattleSideEnum localSide, bool startWithStaleBattle, bool isHideout, bool isDirectAssault)
    {
        var staleMapEvent = startWithStaleBattle ? CreateServerMapEvent() : null;
        var mapEvent = CreateServerMapEvent();
        var nonInitiatingPartyId = JoinNewServerPartyToSide(mapEvent.MapEventId, localSide);
        var troopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        SeedPartyTroopOnAll(nonInitiatingPartyId, troopId, 3);
        var client = Clients.Last();
        var missionInitializerResolver = new RecordingMissionInitializerResolver();
        var battleLauncher = new Mock<ICoopFieldBattleLauncher>();
        var hideoutLauncher = new Mock<ICoopHideoutMissionLauncher>();
        MissionInitializerRecord openedInitializer = default;
        battleLauncher.Setup(l => l.OpenCoopFieldBattle(It.IsAny<MissionInitializerRecord>()))
            .Callback<MissionInitializerRecord>(initializer => openedInitializer = initializer)
            .Returns((Mission)null!);
        hideoutLauncher.Setup(l => l.OpenCoopHideoutMission(It.IsAny<MissionInitializerRecord>(), isDirectAssault))
            .Callback<MissionInitializerRecord, bool>((initializer, _) => openedInitializer = initializer)
            .Returns((Mission)null!);

        using var launcherScope = client.Container.BeginLifetimeScope(builder =>
        {
            builder.RegisterInstance(battleLauncher.Object).As<ICoopFieldBattleLauncher>();
            builder.RegisterInstance(hideoutLauncher.Object).As<ICoopHideoutMissionLauncher>();
        });

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(mapEvent.MapEventId, out var clientBattle));
            if (isHideout)
                clientBattle._mapEventType = MapEvent.BattleTypes.Hideout;
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(nonInitiatingPartyId, out var localParty));
            var previousMainParty = Campaign.Current.MainParty;
            Campaign.Current.MainParty = localParty;
            Campaign.Current.PlayerEncounter = null;

            PlayerEncounter staleEncounter = null;
            if (staleMapEvent != null)
            {
                Assert.True(client.ObjectManager.TryGetObject<MapEvent>(staleMapEvent.MapEventId, out var staleBattle));
                PlayerEncounter.Start();
                staleEncounter = PlayerEncounter.Current;
                staleEncounter._mapEvent = staleBattle;
            }

            Assert.Same(clientBattle, MobileParty.MainParty.MapEvent);
            Assert.NotSame(clientBattle, PlayerEncounter.Battle);

            using var messageBroker = new MessageBroker();
            using var handler = new BattleMissionStartHandler(
                messageBroker,
                client.ObjectManager,
                client.Resolve<IPlayerManager>(),
                client.Resolve<INetwork>(),
                client.Resolve<IMapEventLogger>(),
                missionInitializerResolver,
                null!);

            try
            {
                ContainerProvider.SetContainer(launcherScope);
                var missionInitializer = new MissionInitializerRecord("battle_terrain_030")
                {
                    TerrainType = 7,
                    RandomTerrainSeed = 1234,
                    SceneHasMapPatch = true,
                    PatchCoordinates = new Vec2(0.25f, 0.75f),
                    PatchEncounterDir = new Vec2(1f, 0f),
                };
                messageBroker.Publish(this, new NetworkStartAttackMission(
                    mapEvent.MapEventId, missionInitializer, mapEvent.AttackerPartyId,
                    isHideout, isDirectAssault));

                Assert.Equal(0, missionInitializerResolver.CallCount);
                GameThread.Instance.Update(TimeSpan.FromMilliseconds(16));

                Assert.Equal(0, missionInitializerResolver.CallCount);
                Assert.Equal("battle_terrain_030", openedInitializer.SceneName);
                Assert.Equal(7, openedInitializer.TerrainType);
                Assert.Equal(1234, openedInitializer.RandomTerrainSeed);
                Assert.True(openedInitializer.SceneHasMapPatch);
                Assert.Equal(0.25f, openedInitializer.PatchCoordinates.X);
                Assert.Equal(0.75f, openedInitializer.PatchCoordinates.Y);
                Assert.Equal(1f, openedInitializer.PatchEncounterDir.X);
                Assert.Equal(0f, openedInitializer.PatchEncounterDir.Y);
                Assert.Same(clientBattle, PlayerEncounter.Battle);
                Assert.Equal(localSide, PlayerEncounter.Current.PlayerSide);
                Assert.Equal(localSide.GetOppositeSide(), PlayerEncounter.Current.OpponentSide);
                Assert.Same(localSide == BattleSideEnum.Attacker ? localParty.Party : clientBattle.AttackerSide.LeaderParty,
                    PlayerEncounter.Current._attackerParty);
                Assert.Same(localSide == BattleSideEnum.Defender ? localParty.Party : clientBattle.DefenderSide.LeaderParty,
                    PlayerEncounter.Current._defenderParty);
                Assert.Same(localSide == BattleSideEnum.Attacker ? clientBattle.DefenderSide.LeaderParty : clientBattle.AttackerSide.LeaderParty,
                    PlayerEncounter.EncounteredParty);
                Assert.Same(clientBattle.MapEventSettlement, PlayerEncounter.Current.EncounterSettlementAux);
                Assert.True(PlayerEncounter.Current.PlayerPartyInitialStrength > 0);
                Assert.True(PlayerEncounter.Current.IsJoinedBattle);
                if (staleEncounter != null)
                    Assert.NotSame(staleEncounter, PlayerEncounter.Current);
                Assert.NotNull(new SandboxBattleInitializationModel().GetAllAvailableTroopTypes());
                battleLauncher.Verify(
                    l => l.OpenCoopFieldBattle(It.IsAny<MissionInitializerRecord>()),
                    isHideout ? Times.Never() : Times.Once());
                hideoutLauncher.Verify(
                    l => l.OpenCoopHideoutMission(It.IsAny<MissionInitializerRecord>(), isDirectAssault),
                    isHideout ? Times.Once() : Times.Never());
                Assert.False(BattleSpawnGate.IsCoopBattleActive);
            }
            finally
            {
                BattleSpawnGate.EndBattle();
                Campaign.Current.MainParty = previousMainParty;
                Campaign.Current.PlayerEncounter = null;
                ContainerProvider.SetContainer(client.Container);
            }
        });
    }

    [Theory]
    [InlineData(BattleSideEnum.Defender, false)]
    [InlineData(BattleSideEnum.Attacker, false)]
    [InlineData(BattleSideEnum.Defender, true)]
    public void SiegeMissionStart_InitializesEncounterBeforeOpeningMission(
        BattleSideEnum localSide, bool hasCurrentEncounter)
    {
        var mapEvent = CreateServerMapEvent();
        var partyId = JoinNewServerPartyToSide(mapEvent.MapEventId, localSide);
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var client = Clients.Last();
        var launcher = new Mock<ICoopSiegeBattleLauncher>();
        bool opened = false;

        using var launcherScope = client.Container.BeginLifetimeScope(builder =>
            builder.RegisterInstance(launcher.Object).As<ICoopSiegeBattleLauncher>());

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(mapEvent.MapEventId, out var battle));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            var campaign = Campaign.Current;
            var previousMainParty = campaign.MainParty;
            var previousEncounter = campaign.PlayerEncounter;
            var previousLocationModel = campaign.Models.LocationModel;
            var previousDifficultyModel = campaign.Models.DifficultyModel;
            var previousWeatherModel = campaign.Models.MapWeatherModel;
            var previousScene = campaign._mapSceneWrapper;

            try
            {
                campaign.MainParty = party;
                campaign.PlayerEncounter = null;
                campaign._mapSceneWrapper = Mock.Of<IMapScene>();
                campaign.Models.LocationModel = Mock.Of<LocationModel>();
                campaign.Models.DifficultyModel = Mock.Of<DifficultyModel>();
                campaign.Models.MapWeatherModel = Mock.Of<MapWeatherModel>();
                battle._mapEventType = MapEvent.BattleTypes.Siege;
                battle.MapEventSettlement = settlement;
                settlement.LocationComplex = new LocationComplex();
                settlement.LocationComplex._locations.Add("center", new Location(
                    "center", new TextObject("castle"), new TextObject("gate"), 0, false, false,
                    "CanAlways", "CanAlways", "CanAlways", "CanAlways",
                    new[] { "empire_castle_f", "empire_castle_f", "empire_castle_f", "empire_castle_f" },
                    settlement.LocationComplex));

                if (hasCurrentEncounter)
                    BattleMissionStartHandler.InitializePlayerEncounter(battle);
                var existingEncounter = PlayerEncounter.Current;

                launcher.Setup(l => l.OpenCoopSiegeBattle(It.IsAny<MissionInitializerRecord>(),
                        It.IsAny<float[]>(), It.IsAny<List<MissionSiegeWeapon>>(),
                        It.IsAny<List<MissionSiegeWeapon>>(), false))
                    .Returns(() =>
                    {
                        Assert.Same(battle, PlayerEncounter.Battle);
                        Assert.Equal(localSide == BattleSideEnum.Attacker, PlayerEncounter.PlayerIsAttacker);
                        Assert.Same(settlement, PlayerEncounter.Current.EncounterSettlementAux);
                        Assert.True(PlayerEncounter.Current.IsJoinedBattle);
                        if (hasCurrentEncounter) Assert.Same(existingEncounter, PlayerEncounter.Current);
                        opened = true;
                        return null!;
                    });

                using var messageBroker = new MessageBroker();
                using var handler = new BattleMissionStartHandler(messageBroker, client.ObjectManager,
                    client.Resolve<IPlayerManager>(), client.Resolve<INetwork>(),
                    client.Resolve<IMapEventLogger>(), new RecordingMissionInitializerResolver(), null!);
                ContainerProvider.SetContainer(launcherScope);
                messageBroker.Publish(this, new NetworkStartSiegeMission(mapEvent.MapEventId, 0,
                    new[] { 1f, 1f }, Array.Empty<SiegeEngineState>(), Array.Empty<SiegeEngineState>(),
                    mapEvent.AttackerPartyId, settlementId));
                GameThread.Instance.Update(TimeSpan.FromMilliseconds(16));

                Assert.True(opened);
                Assert.Same(battle, PlayerEncounter.Battle);
                Assert.Equal(localSide == BattleSideEnum.Attacker, PlayerEncounter.PlayerIsAttacker);
                Assert.False(BattleSpawnGate.IsCoopBattleActive);
            }
            finally
            {
                BattleSpawnGate.EndBattle();
                campaign.MainParty = previousMainParty;
                campaign.PlayerEncounter = previousEncounter;
                campaign.Models.LocationModel = previousLocationModel;
                campaign.Models.DifficultyModel = previousDifficultyModel;
                campaign.Models.MapWeatherModel = previousWeatherModel;
                campaign._mapSceneWrapper = previousScene;
                ContainerProvider.SetContainer(client.Container);
            }
        });
    }

    [Fact]
    public void InitializePlayerEncounter_CurrentBattleIsPreserved()
    {
        var mapEvent = CreateServerMapEvent();
        var client = Clients.First();

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(mapEvent.MapEventId, out var clientBattle));
            PlayerEncounter.Start();
            var existingEncounter = PlayerEncounter.Current;
            existingEncounter._mapEvent = clientBattle;
            existingEncounter.FirstInit = false;

            try
            {
                BattleMissionStartHandler.InitializePlayerEncounter(clientBattle);

                Assert.Same(existingEncounter, PlayerEncounter.Current);
                Assert.False(PlayerEncounter.Current.FirstInit);
            }
            finally
            {
                Campaign.Current.PlayerEncounter = null;
            }
        });
    }

    [Fact]
    public void AttackMissionStart_BackReferenceWithoutMapEventParty_DoesNotOpen()
    {
        var mapEvent = CreateServerMapEvent();
        var outsiderPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var client = Clients.First();
        var missionInitializerResolver = new RecordingMissionInitializerResolver();

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(mapEvent.MapEventId, out var clientBattle));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(outsiderPartyId, out var outsiderParty));
            var previousMainParty = Campaign.Current.MainParty;
            Campaign.Current.MainParty = outsiderParty;
            outsiderParty.Party._mapEventSide = clientBattle.AttackerSide;

            try
            {
                Assert.Same(clientBattle, outsiderParty.MapEvent);
                Assert.DoesNotContain(clientBattle.AttackerSide.Parties, party => party.Party == outsiderParty.Party);

                using var messageBroker = new MessageBroker();
                using var handler = new BattleMissionStartHandler(
                    messageBroker,
                    client.ObjectManager,
                    client.Resolve<IPlayerManager>(),
                    client.Resolve<INetwork>(),
                    client.Resolve<IMapEventLogger>(),
                    missionInitializerResolver,
                    null!);

                messageBroker.Publish(this, new NetworkStartAttackMission(
                    mapEvent.MapEventId,
                    new MissionInitializerRecord("battle_terrain_030") { RandomTerrainSeed = 1234 },
                    mapEvent.AttackerPartyId));
                GameThread.Instance.Update(TimeSpan.FromMilliseconds(16));

                Assert.Equal(0, missionInitializerResolver.CallCount);
                Assert.Null(PlayerEncounter.Current);
            }
            finally
            {
                outsiderParty.Party._mapEventSide = null;
                Campaign.Current.MainParty = previousMainParty;
                Campaign.Current.PlayerEncounter = null;
            }
        });
    }

    private sealed class RecordingMissionInitializerResolver : IBattleMissionInitializerResolver
    {
        public int CallCount { get; private set; }

        public MissionInitializerRecord Create(MapEvent mapEvent, int randomTerrainSeed,
            AtmosphereInfo atmosphereOnCampaign)
        {
            CallCount++;
            return default;
        }
    }
}
