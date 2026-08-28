using GameInterface.Services.Locations;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Locations;

public class SettlementTestEnvironmentTests : SettlementTestEnvironment
{
    public SettlementTestEnvironmentTests(ITestOutputHelper output) : base(output, numClients: 2)
    {
    }

    [Fact]
    public void EnterAndLeave_ComposesOneClientLocationMission()
    {
        var client = Clients.First();
        var (instanceId, _) = CreateSettlement("A");

        SettlementClientFixture fixture = EnterLocation(client, instanceId);

        Assert.True(fixture.Mesh.IsStarted);
        Assert.Equal(instanceId, fixture.Mesh.ActiveInstanceId);
        Assert.Same(fixture.PlayerAgent, fixture.Mission.MainAgent);
        client.Call(() =>
        {
            var gate = client.Resolve<ILocationNpcGate>();
            Assert.True(gate.IsCoopLocationMissionActive);
            Assert.Equal(instanceId, gate.ActiveInstanceId);
        });

        LeaveLocation(client);

        Assert.False(fixture.Mesh.IsStarted);
        Assert.Null(fixture.Mesh.ActiveInstanceId);
        client.Call(() => Assert.False(client.Resolve<ILocationNpcGate>().IsCoopLocationMissionActive));
    }

    [Fact]
    public void CompanionStub_CanReplicateThroughTheComposedFixture()
    {
        var clients = Clients.ToArray();
        var (instanceId, _) = CreateSettlement("A", "B");
        SettlementClientFixture first = EnterLocation(clients[0], instanceId);
        SettlementClientFixture second = EnterLocation(clients[1], instanceId);
        var (_, characterId) = CreateHeroCharacter();

        SpawnCompanion(first, characterId, TaleWorlds.Library.Vec3.Zero);
        DrainNetwork();

        Assert.Equal(3, first.Mission.Agents.Count);
        Assert.Equal(3, second.Mission.Agents.Count);
    }

    [Fact]
    public void NpcStub_CanReplicateThroughTheComposedFixture()
    {
        var clients = Clients.ToArray();
        var (instanceId, _) = CreateSettlement("A", "B");
        SettlementClientFixture first = EnterLocation(clients[0], instanceId);
        SettlementClientFixture second = EnterLocation(clients[1], instanceId);
        string characterId = CreateRegisteredObject<TaleWorlds.CampaignSystem.CharacterObject>();

        SpawnNpc(first, characterId, TaleWorlds.Library.Vec3.Zero, TaleWorlds.Library.Vec2.Forward);
        DrainNetwork();
        Tick(0f);

        Assert.Equal(3, first.Mission.Agents.Count);
        Assert.Equal(3, second.Mission.Agents.Count);
    }

    [Fact]
    public void TwoClients_UseSeparateMissionsInTheSameMeshInstance()
    {
        var clients = Clients.ToArray();
        var (instanceId, _) = CreateSettlement("A", "B");

        SettlementClientFixture first = EnterLocation(clients[0], instanceId);
        SettlementClientFixture second = EnterLocation(clients[1], instanceId);
        DrainNetwork();

        Assert.NotSame(first.Mission, second.Mission);
        Assert.Equal(instanceId, first.Mesh.ActiveInstanceId);
        Assert.Equal(instanceId, second.Mesh.ActiveInstanceId);
        Assert.Equal(2, first.Mission.Agents.Count);
        Assert.Equal(2, second.Mission.Agents.Count);

        clients[0].Call(() =>
        {
            Assert.Equal(instanceId, clients[0].Resolve<ILocationNpcGate>().ActiveInstanceId);
            Assert.Equal(instanceId, clients[1].Resolve<ILocationNpcGate>().ActiveInstanceId);
        });
    }
}
