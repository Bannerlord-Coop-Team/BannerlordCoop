using Autofac;
using Common.Messaging;
using Common.Network;
using GameInterface;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Logging;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.Players;
using Moq;
using SandBox.GameComponents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

public class BattleMissionStartHandlerTests : MapEventTestBase
{
    public BattleMissionStartHandlerTests(ITestOutputHelper output) : base(output)
    {
    }

    [Theory]
    [InlineData(BattleSideEnum.Attacker, false)]
    [InlineData(BattleSideEnum.Defender, true)]
    public void AttackMissionStart_NonInitiatingClientInitializesJoinedPlayerEncounter(
        BattleSideEnum localSide, bool startWithStaleBattle)
    {
        var staleMapEvent = startWithStaleBattle ? CreateServerMapEvent() : null;
        var mapEvent = CreateServerMapEvent();
        var nonInitiatingPartyId = JoinNewServerPartyToSide(mapEvent.MapEventId, localSide);
        var troopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        SeedPartyTroopOnAll(nonInitiatingPartyId, troopId, 3);
        var client = Clients.Last();
        var missionInitializerResolver = new RecordingMissionInitializerResolver();
        var battleLauncher = new Mock<ICoopFieldBattleLauncher>();
        battleLauncher.Setup(l => l.OpenCoopFieldBattle(It.IsAny<MissionInitializerRecord>()))
            .Returns((Mission)null!);

        using var launcherScope = client.Container.BeginLifetimeScope(builder =>
            builder.RegisterInstance(battleLauncher.Object).As<ICoopFieldBattleLauncher>());

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(mapEvent.MapEventId, out var clientBattle));
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
                missionInitializerResolver);

            try
            {
                ContainerProvider.SetContainer(launcherScope);
                messageBroker.Publish(this, new NetworkStartAttackMission(
                    mapEvent.MapEventId, 1234, default, mapEvent.AttackerPartyId));

                Assert.Equal(1, missionInitializerResolver.CallCount);
                Assert.Same(clientBattle, missionInitializerResolver.Battle);
                Assert.Equal(1234, missionInitializerResolver.RandomTerrainSeed);
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
                    l => l.OpenCoopFieldBattle(It.IsAny<MissionInitializerRecord>()), Times.Once);
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

    private sealed class RecordingMissionInitializerResolver : IBattleMissionInitializerResolver
    {
        public int CallCount { get; private set; }
        public MapEvent Battle { get; private set; } = null!;
        public int RandomTerrainSeed { get; private set; }

        public MissionInitializerRecord Create(MapEvent mapEvent, int randomTerrainSeed,
            AtmosphereInfo atmosphereOnCampaign)
        {
            CallCount++;
            Battle = mapEvent;
            RandomTerrainSeed = randomTerrainSeed;
            return default;
        }
    }
}
