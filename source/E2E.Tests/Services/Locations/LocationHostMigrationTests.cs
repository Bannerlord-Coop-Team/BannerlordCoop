using E2E.Tests.Environment.Instance;
using E2E.Tests.Environment.MockEngine;
using Missions;
using Missions.Agents.Packets;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Locations;

/// <summary>Exercises NPC continuity across an abrupt location-host migration and later catch-up.</summary>
public class LocationHostMigrationTests : SettlementTestEnvironment
{
    public LocationHostMigrationTests(ITestOutputHelper output) : base(output, numClients: 3)
    {
    }

    [Fact]
    public void AbruptHostDisconnect_AdoptsNpcOnce_RejectsStaleMovement_AndCatchesUpLaterEntrant()
    {
        EnvironmentInstance[] clients = Clients.ToArray();
        var (instanceId, _) = CreateSettlement("A", "B", "C");
        SettlementClientFixture departedHost = EnterLocation(clients[0], instanceId);
        SettlementClientFixture successor = EnterLocation(clients[1], instanceId);
        string characterId = CreateRegisteredObject<CharacterObject>();
        var initialPosition = new Vec3(2f, 3f, 0f);
        var stalePosition = new Vec3(90f, 91f, 0f);

        Agent ownedNpc = SpawnNpc(
            departedHost,
            characterId,
            initialPosition,
            Vec2.Forward);
        DrainNetwork();
        Tick(0f);

        CoopAgentInfo ownedInfo = GetAgentInfo(departedHost, ownedNpc);
        CoopAgentInfo mirroredInfo = GetAgentInfo(successor, ownedInfo.AgentId);
        Agent adoptedNpc = mirroredInfo.Agent;

        Assert.NotSame(ownedNpc, adoptedNpc);
        Assert.Equal("A", ownedInfo.CurrentAuthority);
        Assert.Equal("A", ownedInfo.OriginalOwner);
        Assert.Equal(ownedInfo.AgentId, mirroredInfo.AgentId);
        Assert.Equal("A", mirroredInfo.CurrentAuthority);
        Assert.Equal("A", mirroredInfo.OriginalOwner);
        Assert.Equal(characterId, GetCharacterId(successor, adoptedNpc));
        Assert.Equal(3, departedHost.Mission.Agents.Count);
        Assert.Equal(3, successor.Mission.Agents.Count);

        departedHost.Mesh.RoutePackets = false;
        MoveAgent(ownedNpc, stalePosition, Vec2.Forward);
        departedHost.Tick(0.1f);
        MovementPacket staleMovement = departedHost.Mesh.NetworkSentPackets
            .GetPackets<MovementPacket>()
            .Last(packet =>
                packet.IdentityScopeId == ownedInfo.MovementScopeId &&
                packet.AgentIds != null &&
                packet.AgentIds.Contains(ownedInfo.MovementId));
        int staleNpcIndex = Array.IndexOf(staleMovement.AgentIds, ownedInfo.MovementId);
        Assert.Equal(stalePosition, staleMovement.Agents[staleNpcIndex].Position);

        Disconnect(clients[0]);

        AssertLocationHost(Server, instanceId, "B");
        AssertLocationHost(clients[1], instanceId, "B");
        CoopAgentInfo adoptedInfo = GetAgentInfo(successor, ownedInfo.AgentId);
        Assert.Same(adoptedNpc, adoptedInfo.Agent);
        Assert.Equal("B", adoptedInfo.CurrentAuthority);
        Assert.Equal("A", adoptedInfo.OriginalOwner);
        Assert.Equal(ownedInfo.AuthorityRevision + 1, adoptedInfo.AuthorityRevision);
        Assert.Equal(AgentControllerType.AI, GetAgentState(adoptedNpc).Controller);
        Assert.Equal(initialPosition, GetAgentState(adoptedNpc).Position);
        Assert.Equal(2, GetActiveAgentCount(successor));
        Assert.Single(GetAgentsWithId(successor, ownedInfo.AgentId));

        clients[1].SimulatePacket(departedHost.Mesh.NetPeer, staleMovement);
        successor.Tick(0.1f);

        Assert.Equal(initialPosition, GetAgentState(adoptedNpc).Position);
        Assert.Same(adoptedNpc, GetAgentInfo(successor, ownedInfo.AgentId).Agent);
        Assert.Single(GetAgentsWithId(successor, ownedInfo.AgentId));

        SettlementClientFixture entrant = EnterLocation(clients[2], instanceId);
        DrainNetwork();
        Tick(0f);

        CoopAgentInfo entrantInfo = GetAgentInfo(entrant, ownedInfo.AgentId);
        Assert.NotSame(adoptedNpc, entrantInfo.Agent);
        Assert.Equal(ownedInfo.AgentId, entrantInfo.AgentId);
        Assert.Equal("B", entrantInfo.CurrentAuthority);
        Assert.Equal("A", entrantInfo.OriginalOwner);
        Assert.Equal(adoptedInfo.AuthorityRevision, entrantInfo.AuthorityRevision);
        Assert.Equal(characterId, GetCharacterId(entrant, entrantInfo.Agent));
        Assert.Equal(initialPosition, GetAgentState(entrantInfo.Agent).Position);
        Assert.Equal(3, GetActiveAgentCount(successor));
        Assert.Equal(3, GetActiveAgentCount(entrant));
        Assert.Single(GetAgentsWithId(successor, ownedInfo.AgentId));
        Assert.Single(GetAgentsWithId(entrant, ownedInfo.AgentId));
    }

    private static CoopAgentInfo GetAgentInfo(SettlementClientFixture fixture, Agent agent)
    {
        CoopAgentInfo info = null;
        fixture.Instance.Call(() =>
        {
            Assert.True(fixture.Instance.Resolve<INetworkAgentRegistry>()
                .TryGetAgentInfo(agent, out info));
        });
        return info;
    }

    private static CoopAgentInfo GetAgentInfo(SettlementClientFixture fixture, Guid agentId)
    {
        CoopAgentInfo info = null;
        fixture.Instance.Call(() =>
        {
            Assert.True(fixture.Instance.Resolve<INetworkAgentRegistry>()
                .TryGetAgentInfo(agentId, out info));
        });
        return info;
    }

    private static CoopAgentInfo[] GetAgentsWithId(
        SettlementClientFixture fixture,
        Guid agentId)
    {
        CoopAgentInfo[] matches = null;
        fixture.Instance.Call(() =>
        {
            INetworkAgentRegistry registry = fixture.Instance.Resolve<INetworkAgentRegistry>();
            matches = registry
                .GetControllerIds()
                .SelectMany(registry.GetAgents)
                .Where(info => info.AgentId == agentId)
                .ToArray();
        });
        return matches;
    }

    private int GetActiveAgentCount(SettlementClientFixture fixture)
    {
        return fixture.Mission.Agents.Count(agent => GetAgentState(agent).IsActive);
    }

    private static string GetCharacterId(SettlementClientFixture fixture, Agent agent)
    {
        string characterId = null;
        fixture.Instance.Call(() =>
        {
            Assert.IsType<CharacterObject>(agent.Character);
            Assert.True(fixture.Instance.ObjectManager.TryGetId(
                (CharacterObject)agent.Character,
                out characterId));
        });
        return characterId;
    }
}
