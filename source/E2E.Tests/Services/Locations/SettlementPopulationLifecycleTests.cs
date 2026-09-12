using Coop.Core.Server.Services.Instances;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Locations;
using GameInterface.Services.Locations.Messages;
using Missions.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.MountAndBlade;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Locations;

/// <summary>Exercises settlement population and location mission lifetime through the composed fixture.</summary>
public class SettlementPopulationLifecycleTests : SettlementTestEnvironment
{
    public SettlementPopulationLifecycleTests(ITestOutputHelper output) : base(output, numClients: 2)
    {
    }

    [Fact]
    public void DedicatedServer_RemainsMissionlessAndAgentlessDuringClientLocationPopulation()
    {
        EnvironmentInstance client = Clients.First();
        var (instanceId, _) = CreateSettlement("A");

        SettlementClientFixture fixture = EnterLocation(
            client,
            instanceId,
            enableNativePopulationBoundary: true);

        Assert.Single(fixture.Mission.Agents);
        Server.Call(() =>
        {
            Assert.Null(Mission.Current);
            Assert.Null(Agent.Main);
            Assert.Null(CampaignMission.Current);
        });
    }

    [Fact]
    public void NativeRosterPopulation_OnlyRunsOnConfirmedHostAndReplicatesRosterBoundPuppet()
    {
        EnvironmentInstance[] clients = Clients.ToArray();
        var (instanceId, _) = CreateSettlement("A", "B");
        string characterId = CreateRegisteredObject<CharacterObject>();
        AddAmbientLocationCharacter(instanceId, characterId);

        SettlementClientFixture host = EnterLocation(
            clients[0],
            instanceId,
            enableNativePopulationBoundary: true);
        SettlementClientFixture peer = EnterLocation(
            clients[1],
            instanceId,
            enableNativePopulationBoundary: true);

        RunNativePopulation(peer);
        host.Tick(0f);
        DrainNetwork();
        Tick(0f);

        Assert.Equal(1, host.Mission.NativeLocationPopulationCalls);
        Assert.True(host.Mission.NativeLocationAnimalPopulationCalls > 0);
        Assert.Equal(0, peer.Mission.NativeLocationPopulationCalls);
        Assert.Equal(0, peer.Mission.NativeLocationAnimalPopulationCalls);
        clients[0].Call(() =>
        {
            Assert.True(clients[0].Resolve<ILocationNpcGate>().IsLocalHostConfirmed);
            Assert.False(clients[0].Resolve<ILocationNpcGate>().ShouldSuppressNativeSpawns);
        });
        clients[1].Call(() =>
        {
            Assert.False(clients[1].Resolve<ILocationNpcGate>().IsLocalHostConfirmed);
            Assert.True(clients[1].Resolve<ILocationNpcGate>().ShouldSuppressNativeSpawns);
        });

        Assert.Single(clients[0].InternalMessages.GetMessages<AgentSpawnedInLocation>());
        Assert.NotEmpty(clients[1].InternalMessages.GetMessages<NetworkSpawnLocationAgents>());
        AssertRosterBoundNpc(host, characterId);
        AssertRosterBoundNpc(peer, characterId);
    }

    [Fact]
    public void CampaignSettlementAndLocationEntryExit_UpdateTheirAuthoritativeMemberships()
    {
        EnvironmentInstance client = Clients.First();
        var (instanceId, partyIds) = CreateSettlement("A");

        LeaveCampaignSettlement("A");
        AssertPartySettlement(partyIds[0], expectedSettlementId: null);

        EnterCampaignSettlement("A", instanceId);
        AssertPartySettlement(partyIds[0], instanceId.Split('|')[0]);

        EnterLocation(client, instanceId);
        AssertMissionMembers(instanceId, "A");

        LeaveLocation(client);
        AssertMissionMembers(instanceId);

        LeaveCampaignSettlement("A");
        AssertPartySettlement(partyIds[0], expectedSettlementId: null);
    }

    [Fact]
    public void SecondActiveLocation_IsRejectedWithoutDisturbingCurrentMission()
    {
        EnvironmentInstance client = Clients.First();
        var (firstInstanceId, _) = CreateSettlement("A");
        string settlementId = firstInstanceId.Split('|')[0];
        string secondLocationId = CreateRegisteredObject<Location>();
        string secondInstanceId = $"{settlementId}|{secondLocationId}";
        SettlementClientFixture active = EnterLocation(client, firstInstanceId);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => EnterLocation(client, secondInstanceId));

        Assert.Equal("A client can only have one active settlement location", error.Message);
        Assert.True(active.Mesh.IsStarted);
        Assert.Equal(firstInstanceId, active.Mesh.ActiveInstanceId);
        AssertMissionMembers(firstInstanceId, "A");
        AssertMissionMembers(secondInstanceId);
    }

    [Fact]
    public void GracefulLeaveAndReentry_CreateCleanMissionAndGateState()
    {
        EnvironmentInstance client = Clients.First();
        var (instanceId, _) = CreateSettlement("A");
        SettlementClientFixture first = EnterLocation(
            client,
            instanceId,
            enableNativePopulationBoundary: true);

        Assert.Equal(1, first.Mission.NativeLocationPopulationCalls);
        AssertMissionMembers(instanceId, "A");

        LeaveLocation(client);

        Assert.False(first.Mesh.IsStarted);
        Assert.Null(first.Mesh.ActiveInstanceId);
        AssertMissionMembers(instanceId);
        AssertCleanGate(client, activeInstanceId: null, localHostConfirmed: false);

        SettlementClientFixture second = EnterLocation(
            client,
            instanceId,
            enableNativePopulationBoundary: true);

        Assert.NotSame(first.Mission, second.Mission);
        Assert.NotSame(first.Controller, second.Controller);
        Assert.Single(second.Mission.Agents);
        Assert.Equal(1, second.Mission.NativeLocationPopulationCalls);
        AssertMissionMembers(instanceId, "A");
        AssertCleanGate(client, instanceId, localHostConfirmed: true);
    }

    private static void AssertRosterBoundNpc(SettlementClientFixture fixture, string characterId)
    {
        fixture.Instance.Call(() =>
        {
            CharacterObject character = fixture.Instance.GetRegisteredObject<CharacterObject>(characterId);
            LocationCharacter rosterEntry = Assert.Single(
                fixture.Location.GetCharacterList(),
                entry => ReferenceEquals(entry.Character, character));
            Agent npc = Assert.Single(
                fixture.Mission.Agents,
                agent => ReferenceEquals(agent.Character, character));

            Assert.Same(rosterEntry.AgentOrigin, npc.Origin);
        });
    }

    private void AssertPartySettlement(string partyId, string? expectedSettlementId)
    {
        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<TaleWorlds.CampaignSystem.Party.MobileParty>(partyId);
            if (expectedSettlementId == null)
            {
                Assert.Null(party.CurrentSettlement);
                return;
            }

            var settlement = Server.GetRegisteredObject<TaleWorlds.CampaignSystem.Settlements.Settlement>(
                expectedSettlementId);
            Assert.Same(settlement, party.CurrentSettlement);
        });
    }

    private void AssertMissionMembers(string instanceId, params string[] expectedControllerIds)
    {
        Server.Call(() =>
        {
            IMissionManager manager = Server.Resolve<IMissionManager>();
            if (expectedControllerIds.Length == 0)
            {
                Assert.False(manager.TryGetControllers(instanceId, out _));
                return;
            }

            Assert.True(manager.TryGetControllers(instanceId, out var controllers));
            Assert.Equal(expectedControllerIds, controllers.OrderBy(value => value).ToArray());
        });
    }

    private static void AssertCleanGate(
        EnvironmentInstance client,
        string? activeInstanceId,
        bool localHostConfirmed)
    {
        client.Call(() =>
        {
            ILocationNpcGate gate = client.Resolve<ILocationNpcGate>();
            Assert.Equal(activeInstanceId, gate.ActiveInstanceId);
            Assert.Equal(activeInstanceId != null, gate.IsCoopLocationMissionActive);
            Assert.Equal(localHostConfirmed, gate.IsLocalHostConfirmed);
            Assert.False(gate.SuppressCapture);
            Assert.False(gate.IsReplayingNativePopulation);
        });
    }
}
