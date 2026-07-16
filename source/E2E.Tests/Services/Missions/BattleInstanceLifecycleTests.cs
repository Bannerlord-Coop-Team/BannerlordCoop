using System.Linq;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents;
using Missions;
using Missions.Battles;
using Missions.Messages;
using Missions.Services.Network;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

public class BattleInstanceLifecycleTests : MissionTestEnvironment
{
    public BattleInstanceLifecycleTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    [Trait("Requirement", "BR-054")]
    public void Leave_ClearsLocalMissionMembershipBeforeReentry()
    {
        var (mapEventId, _) = SetupCoopBattle("A", "B");
        var client = Clients.First();

        client.Call(() =>
        {
            var broker = client.Resolve<IMessageBroker>();
            var context = client.Resolve<IMissionContext>();
            broker.Publish(this, new NetworkMissionPeerEntered("B", mapEventId));
            Assert.Contains("B", context.ControllersInMission);

            var session = new BattleSession(
                client.Resolve<IControllerIdProvider>(),
                client.Resolve<IBattleHostRegistry>());
            session.TryBegin(mapEventId);

            using var lifecycle = new BattleInstanceLifecycle(
                client.Resolve<IBattleNetwork>(),
                client.Resolve<INetwork>(),
                broker,
                objectManager: null,
                coopMissionComponent: null,
                session,
                context);

            lifecycle.Leave();

            Assert.Empty(context.ControllersInMission);
        });
    }
}
