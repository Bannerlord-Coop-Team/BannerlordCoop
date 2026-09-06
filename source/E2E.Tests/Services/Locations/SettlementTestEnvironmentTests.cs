using GameInterface.Services.Locations;
using GameInterface.Services.Locations.Conversations;
using GameInterface.Services.Locations.Conversations.Patches;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Locations;

public class SettlementTestEnvironmentTests : SettlementTestEnvironment
{
    private static readonly MethodInfo ReleaseConversationState =
        typeof(LocationConversationPatches).GetMethod(
            "ReleaseStaleLock",
            BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(typeof(LocationConversationPatches).FullName, "ReleaseStaleLock");

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

    [Fact]
    public void LeavingOneClient_DoesNotClearOtherClientNpcGate()
    {
        var clients = Clients.ToArray();
        var (instanceId, _) = CreateSettlement("A", "B");
        SettlementClientFixture first = EnterLocation(clients[0], instanceId);
        SettlementClientFixture second = EnterLocation(clients[1], instanceId);

        clients[0].Call(() =>
        {
            Assert.True(LocationNpcGate.IsCoopLocationMissionActive);
            Assert.Equal(first.InstanceId, LocationNpcGate.ActiveInstanceId);
        });
        clients[1].Call(() =>
        {
            Assert.True(LocationNpcGate.IsCoopLocationMissionActive);
            Assert.Equal(second.InstanceId, LocationNpcGate.ActiveInstanceId);
        });

        LeaveLocation(clients[0]);

        clients[0].Call(() => Assert.False(LocationNpcGate.IsCoopLocationMissionActive));
        clients[1].Call(() =>
        {
            Assert.True(LocationNpcGate.IsCoopLocationMissionActive);
            Assert.Equal(second.InstanceId, LocationNpcGate.ActiveInstanceId);
        });
    }

    [Fact]
    public void ConversationPatch_ClearUsesActiveClientState()
    {
        var clients = Clients.ToArray();
        var (instanceId, _) = CreateSettlement("A", "B");
        SettlementClientFixture first = EnterLocation(clients[0], instanceId);
        SettlementClientFixture second = EnterLocation(clients[1], instanceId);
        int secondGeneration = 0;

        clients[0].Call(() =>
        {
            var state = clients[0].Resolve<ILocationConversationClientState>();
            Assert.True(state.TryBeginPending(
                first.PlayerAgent,
                "first-location",
                "first-character",
                out var firstGeneration));
            Assert.True(state.TryTakePending(firstGeneration, out var pending));
            Assert.Equal("first-location", pending.LocationId);
            Assert.Equal("first-character", pending.CharacterId);
            state.Hold("first-location|first-character");
        });
        clients[1].Call(() =>
        {
            var state = clients[1].Resolve<ILocationConversationClientState>();
            Assert.True(state.TryBeginPending(
                second.PlayerAgent,
                "second-location",
                "second-character",
                out secondGeneration));
        });

        clients[0].Call(() => ReleaseConversationState.Invoke(null, null));

        clients[0].Call(() =>
            Assert.False(clients[0].Resolve<ILocationConversationClientState>().HasPendingOrHeld));
        clients[1].Call(() =>
        {
            var state = clients[1].Resolve<ILocationConversationClientState>();
            Assert.True(state.HasPendingOrHeld);
            Assert.Null(state.HeldNpcKey);
            Assert.True(state.TryTakePending(secondGeneration, out var pending));
            Assert.Equal("second-location", pending.LocationId);
            Assert.Equal("second-character", pending.CharacterId);
            Assert.Same(second.PlayerAgent, pending.Agent);
            Assert.False(state.HasPendingOrHeld);
        });
    }

    [Fact]
    public void ClientCall_SwitchesAndRestoresCampaignMission()
    {
        var clients = Clients.ToArray();
        var (instanceId, _) = CreateSettlement("A", "B");
        SettlementClientFixture first = EnterLocation(clients[0], instanceId);
        SettlementClientFixture second = EnterLocation(clients[1], instanceId);
        ICampaignMission firstCampaignMission = clients[0].CampaignMissionContext;
        ICampaignMission secondCampaignMission = clients[1].CampaignMissionContext;
        ICampaignMission previousCampaignMission = CampaignMission.Current;

        Assert.NotSame(first.Location, second.Location);
        clients[0].Call(() =>
        {
            Assert.Same(firstCampaignMission, CampaignMission.Current);
            Assert.Same(first.Location, CampaignMission.Current.Location);

            clients[1].Call(() =>
            {
                Assert.Same(secondCampaignMission, CampaignMission.Current);
                Assert.Same(second.Location, CampaignMission.Current.Location);
            });

            Assert.Same(firstCampaignMission, CampaignMission.Current);
            Assert.Same(first.Location, CampaignMission.Current.Location);
        });
        Assert.Same(previousCampaignMission, CampaignMission.Current);

        LeaveLocation(clients[0]);
        clients[1].Call(() =>
        {
            Assert.Same(secondCampaignMission, CampaignMission.Current);
            Assert.Same(second.Location, CampaignMission.Current.Location);
        });
    }
}
