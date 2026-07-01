using Missions;
using Missions.Messages;
using System;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// Validates the in-process mission-mesh harness (<c>MockBattleNetwork</c> + <c>MeshNetworkRouter</c>):
/// <see cref="IBattleNetwork"/> traffic routes between client instances the way the real P2P mesh would,
/// so future spawn / control-transfer tests can ride it. (The campaign <c>INetwork</c> is covered by the
/// rest of the E2E suite.)
/// </summary>
public class BattleMeshNetworkTests : MissionTestEnvironment
{
    public BattleMeshNetworkTests(ITestOutputHelper output) : base(output) { }

    private static NetworkSpawnBattleAgents EmptySpawn() =>
        new NetworkSpawnBattleAgents(Array.Empty<BattleAgentSpawnData>());

    [Fact]
    public void Mesh_SendAll_DeliversToOtherClients_ButNotSender()
    {
        var clients = Clients.ToArray();
        SetControllerId(clients[0], "ctrl-A");
        SetControllerId(clients[1], "ctrl-B");

        clients[0].Call(() => clients[0].Resolve<IBattleNetwork>().SendAll(EmptySpawn()));

        // The mesh broadcast reaches peers, not the sender (the real mesh SendAll targets connected peers only).
        Assert.Equal(0, clients[0].InternalMessages.GetMessageCount<NetworkSpawnBattleAgents>());
        Assert.Equal(1, clients[1].InternalMessages.GetMessageCount<NetworkSpawnBattleAgents>());
    }

    [Fact]
    public void Mesh_Send_DeliversOnlyToTargetController()
    {
        var clients = Clients.ToArray();
        SetControllerId(clients[0], "ctrl-A");
        SetControllerId(clients[1], "ctrl-B");

        clients[0].Call(() => clients[0].Resolve<IBattleNetwork>().Send("ctrl-B", EmptySpawn()));

        Assert.Equal(1, clients[1].InternalMessages.GetMessageCount<NetworkSpawnBattleAgents>());
        Assert.Equal(0, clients[0].InternalMessages.GetMessageCount<NetworkSpawnBattleAgents>());
    }
}
