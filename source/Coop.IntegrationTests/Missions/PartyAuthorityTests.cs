using Common.Network.Messages;
using Coop.Core.Server;
using Coop.Core.Server.Services.Instances.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Missions;
using GameInterface.Missions.Services.Network.Messages;

namespace Coop.IntegrationTests.Missions;

/// <summary>
/// E2E coverage of the party control-authority protocol. A spawned party is owned by its spawner on every
/// node; an owner's disconnect hands its party to the host (owner kept, authority = host); the same owner's
/// rejoin hands it back. Authority is read from each node's <see cref="IMissionPartyRegistry"/> — the host
/// arbitrates and every node converges via <see cref="PartyControlChanged"/>.
/// </summary>
public class PartyAuthorityTests
{
    private const string Host = CoopServer.ServerControllerId;
    private const string InstanceId = "Settlement|Location";

    internal TestEnvironment TestEnvironment { get; } = new TestEnvironment(numClients: 3);

    private EnvironmentInstance Server => TestEnvironment.Server;

    // --- Lifecycle: spawn states ---

    [Fact]
    public void ClientSpawnedParty_IsClientOwned_OnAllNodes()
    {
        var members = SetupClients();
        var owner = members[0];
        var partyId = Guid.NewGuid();

        SpawnClientParty(owner, partyId);

        AssertPartyAuthority(partyId, owner.OwnerId, owner.OwnerId, AllNodes(members));
    }

    [Fact]
    public void HostSpawnedNpcParty_IsHostOwned_OnAllNodes()
    {
        var members = SetupClients();
        var partyId = Guid.NewGuid();

        SpawnHostParty(partyId);

        AssertPartyAuthority(partyId, Host, Host, AllNodes(members));
    }

    // --- Disconnect -> takeover ---

    [Fact]
    public void OnDisconnect_ClientPartyBecomesHostHeld_OwnerKept()
    {
        var members = SetupClients();
        var owner = members[0];
        var partyId = Guid.NewGuid();
        SpawnClientParty(owner, partyId);

        Disconnect(owner);

        // CurrentAuthority -> host, OriginalOwner kept. Assert on the host and the still-connected clients.
        AssertPartyAuthority(partyId, Host, owner.OwnerId, new[] { Server, members[1].Instance, members[2].Instance });
    }

    // --- Rejoin -> reclaim ---

    [Fact]
    public void OnRejoin_HostHeldPartyReturnsToOwner_OnAllNodes()
    {
        var members = SetupClients();
        var owner = members[0];
        var partyId = Guid.NewGuid();
        SpawnClientParty(owner, partyId);
        Disconnect(owner);

        Rejoin(owner);

        AssertPartyAuthority(partyId, owner.OwnerId, owner.OwnerId, AllNodes(members));

        // A state snapshot is sent to the rejoining owner before control returns.
        Assert.True(Server.NetworkSentMessages.GetMessageCount<PartyStateSnapshot>() >= 1);
    }

    // --- Helpers ---

    private record Member(EnvironmentInstance Instance, string OwnerId);

    private List<Member> SetupClients()
    {
        var members = new List<Member>();
        int i = 0;
        foreach (var client in TestEnvironment.Clients)
            members.Add(new Member(client, $"Client{++i}"));
        return members;
    }

    private EnvironmentInstance[] AllNodes(IEnumerable<Member> members) =>
        new[] { Server }.Concat(members.Select(m => m.Instance)).ToArray();

    /// <summary>A client announces its own party; the server records it and relays to every node.</summary>
    private void SpawnClientParty(Member owner, Guid partyId) =>
        Server.SimulateMessage(owner.Instance.NetPeer,
            new PartySpawned(partyId, owner.OwnerId, Guid.NewGuid(), new[] { Guid.NewGuid(), Guid.NewGuid() }));

    /// <summary>The host announces an NPC party (no owning connection).</summary>
    private void SpawnHostParty(Guid partyId) =>
        Server.SimulateMessage(this, new PartySpawned(partyId, Host, Guid.NewGuid(), new[] { Guid.NewGuid() }));

    private void Disconnect(Member owner) =>
        Server.SimulateMessage(this, new PlayerDisconnected(owner.Instance.NetPeer, default));

    private void Rejoin(Member owner) =>
        Server.SimulateMessage(owner.Instance.NetPeer, new MissionEntered(owner.OwnerId, InstanceId));

    private void AssertPartyAuthority(Guid partyId, string authority, string owner, IEnumerable<EnvironmentInstance> nodes)
    {
        foreach (var node in nodes)
        {
            var registry = node.Resolve<IMissionPartyRegistry>();
            Assert.True(registry.TryGetParty(partyId, out var party), $"Party {partyId} missing on a node");
            Assert.Equal(owner, party.OriginalOwner);
            Assert.Equal(authority, party.CurrentAuthority);
        }
    }
}
