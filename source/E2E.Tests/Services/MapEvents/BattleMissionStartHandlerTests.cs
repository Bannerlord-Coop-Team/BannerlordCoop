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

    [Fact]
    public void AttackMissionStart_NonInitiatingClientUsesMainPartyMapEventWithoutPlayerEncounter()
    {
        var mapEvent = CreateServerMapEvent();
        var nonInitiatingPartyId = JoinNewServerPartyToSide(mapEvent.MapEventId, BattleSideEnum.Attacker);
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
            Campaign.Current.MainParty = localParty;
            Campaign.Current.PlayerEncounter = null;

            Assert.Same(clientBattle, MobileParty.MainParty.MapEvent);
            Assert.Null(Campaign.Current.PlayerEncounter);
            Assert.Null(PlayerEncounter.Battle);

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
                battleLauncher.Verify(
                    l => l.OpenCoopFieldBattle(It.IsAny<MissionInitializerRecord>()), Times.Once);
                Assert.False(BattleSpawnGate.IsCoopBattleActive);
            }
            finally
            {
                BattleSpawnGate.EndBattle();
                ContainerProvider.SetContainer(client.Container);
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
