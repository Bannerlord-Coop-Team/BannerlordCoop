using Common.Messaging;
using Coop.Core.Server.Services.Instances;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Entity;
using Missions.Messages;
using Missions.Services.Network;

namespace Coop.IntegrationTests.Missions;

/// <summary>
/// Verifies the server's mission-instance membership (MissionInstance, queried via <see cref="IMissionManager"/>)
/// and each client's mirror (<see cref="MissionContext"/>) converge to the same set of controllers as clients
/// join and leave an instance. Each client's view excludes its own controller id, so "equivalent" means: the
/// server lists every present controller, and each client lists exactly the others.
/// </summary>
public class MissionMembershipTests
{
    private const string InstanceId = "Settlement|Location";

    // 1 server + 3 clients, so "one or more already present" exercises multiple existing members.
    internal TestEnvironment TestEnvironment { get; } = new TestEnvironment(numClients: 3);

    [Fact]
    public void ServerAndClientControllers_AreEquivalent_AfterAllJoin()
    {
        var members = SetupClients();

        foreach (var member in members)
            Join(member);

        AssertControllersEquivalent(members);
    }

    [Fact]
    public void NewClientJoins_WithExistingMembers_ControllersEquivalentOnAll()
    {
        var members = SetupClients();

        // Two members already in the instance...
        Join(members[0]);
        Join(members[1]);

        // ...then a new client joins.
        Join(members[2]);

        AssertControllersEquivalent(members);
    }

    [Fact]
    public void ClientLeaves_WithExistingMembers_ControllersEquivalentOnAll()
    {
        var members = SetupClients();

        foreach (var member in members)
            Join(member);

        // One member leaves; the rest remain.
        Leave(members[2]);

        AssertControllersEquivalent(members.Take(2).ToList());

        // The leaver's own mirror is dropped with the instance — the server never tells a departed member
        // about later departures, so anything kept here would go stale.
        Assert.Empty(members[2].Instance.Resolve<MissionContext>().ControllersInMission);
    }

    [Fact]
    public void Membership_DoesNotLeakIntoTheNextInstance()
    {
        const string NextInstanceId = "MapEvent_Next";
        var members = SetupClients();

        // Two members share an instance; one leaves first. (At battle end everyone leaves at once — the
        // server only fans a departure out to members still in its table, so whoever it processes first is
        // never told about the later leavers.)
        Join(members[0]);
        Join(members[1]);
        Leave(members[0]);

        // The early leaver then enters a different instance alone.
        Join(members[0], NextInstanceId);

        // Nothing from the previous instance may linger in its mirror: a lingering member here made every
        // broadcast of the new mission relay at a controller the server has no mapping for in this instance
        // ("Failed to get peer for instance (...) controller (...)").
        Assert.Empty(members[0].Instance.Resolve<MissionContext>().ControllersInMission);

        // And the server-side lookup those relays would perform: only the actual member resolves.
        var missionManager = TestEnvironment.Server.Resolve<IMissionManager>();
        Assert.True(missionManager.TryGetControllers(NextInstanceId, out var controllers));
        Assert.Equal(new[] { members[0].ControllerId }, controllers.ToArray());
        Assert.False(missionManager.TryGetRelayTarget(NextInstanceId, members[1].ControllerId, out _));
        Assert.True(missionManager.TryGetRelayTarget(NextInstanceId, members[0].ControllerId, out _));
    }

    [Fact]
    public void CrossInstanceIntroduction_IsIgnored()
    {
        var members = SetupClients();

        Join(members[0]);
        Join(members[1]);

        // A stray introduction for a DIFFERENT instance (stale, or in flight while its recipient moved on)
        // must not enter the mirror — broadcasts would relay at a controller the server has no mapping for
        // in this instance.
        members[0].Instance.Resolve<IMessageBroker>().Publish(this,
            new NetworkMissionPeerEntered("Stranger", "SomeOtherInstance"));

        var context = members[0].Instance.Resolve<MissionContext>();
        Assert.Equal(new[] { members[1].ControllerId }, context.ControllersInMission.ToArray());
    }

    [Fact]
    public void EnterMission_EvictsTheControllerFromItsPriorInstance()
    {
        const string NextInstanceId = "MapEvent_Next";
        var members = SetupClients();

        // A mission whose teardown died never announces its leave; the member then enters another instance.
        Join(members[0]);
        Join(members[0], NextInstanceId);

        // A controller is in at most one instance: the stale mapping is evicted, so relays into the OLD
        // instance no longer resolve and only the new instance routes to it.
        var missionManager = TestEnvironment.Server.Resolve<IMissionManager>();
        Assert.True(missionManager.TryGetControllers(InstanceId, out var oldControllers));
        Assert.Empty(oldControllers);
        Assert.False(missionManager.TryGetRelayTarget(InstanceId, members[0].ControllerId, out _));
        Assert.True(missionManager.TryGetRelayTarget(NextInstanceId, members[0].ControllerId, out _));
    }

    [Fact]
    public void ClientLeaves_DepartureMarksWhetherInstanceIsEmpty()
    {
        var members = SetupClients().Take(2).ToArray();
        var departures = new List<MissionMemberDeparted>();
        var messageBroker = TestEnvironment.Server.Resolve<IMessageBroker>();
        messageBroker.Subscribe<MissionMemberDeparted>(payload => departures.Add(payload.What));

        Join(members[0]);
        Join(members[1]);

        Leave(members[1]);

        var firstDeparture = Assert.Single(departures);
        Assert.Equal(members[1].ControllerId, firstDeparture.ControllerId);
        Assert.Equal(InstanceId, firstDeparture.InstanceId);
        Assert.True(firstDeparture.WasRetreat);
        Assert.False(firstDeparture.IsInstanceEmpty);

        departures.Clear();
        Leave(members[0]);

        var lastDeparture = Assert.Single(departures);
        Assert.Equal(members[0].ControllerId, lastDeparture.ControllerId);
        Assert.Equal(InstanceId, lastDeparture.InstanceId);
        Assert.True(lastDeparture.WasRetreat);
        Assert.True(lastDeparture.IsInstanceEmpty);
    }

    private record Member(EnvironmentInstance Instance, string ControllerId);

    /// <summary>Assigns each client a distinct controller id (the id MissionContext filters itself out by).</summary>
    private List<Member> SetupClients()
    {
        var members = new List<Member>();
        int i = 0;
        foreach (var client in TestEnvironment.Clients)
        {
            var controllerId = $"Client{++i}";
            client.Resolve<IControllerIdProvider>().SetControllerId(controllerId);
            members.Add(new Member(client, controllerId));
        }
        return members;
    }

    /// <summary>
    /// Simulates the member entering an instance the way the real client does: the mission connect scopes its
    /// MissionContext to the instance (LiteNetP2PClient.ConnectToInstance → BeginInstance), then the entry is
    /// announced to the server.
    /// </summary>
    private void Join(Member member, string instanceId = InstanceId)
    {
        member.Instance.Resolve<MissionContext>().BeginInstance(instanceId);
        TestEnvironment.Server.SimulateMessage(member.Instance.NetPeer, new NetworkMissionEntered(member.ControllerId, instanceId));
    }

    /// <summary>
    /// Simulates the member leaving an instance the way the real client does: the departure is announced to
    /// the server, then the mission network teardown drops the membership mirror
    /// (LiteNetP2PClient.DisconnectPeers → EndInstance).
    /// </summary>
    private void Leave(Member member, string instanceId = InstanceId)
    {
        TestEnvironment.Server.SimulateMessage(member.Instance.NetPeer, new NetworkMissionLeft(member.ControllerId, instanceId));
        member.Instance.Resolve<MissionContext>().EndInstance();
    }

    /// <summary>
    /// Asserts the server's instance controllers equal the present members, and each present member's
    /// MissionContext sees exactly the other present members (its own id excluded).
    /// </summary>
    private void AssertControllersEquivalent(IReadOnlyList<Member> present)
    {
        var presentIds = present.Select(m => m.ControllerId).OrderBy(id => id).ToList();

        // Server view: MissionInstance.Controllers via the manager.
        var missionManager = TestEnvironment.Server.Resolve<IMissionManager>();
        Assert.True(missionManager.TryGetControllers(InstanceId, out var serverControllers));
        Assert.Equal(presentIds, serverControllers.OrderBy(id => id).ToList());

        // Each client view: MissionContext == the server set minus the client itself.
        foreach (var member in present)
        {
            var context = member.Instance.Resolve<MissionContext>();
            var expected = present
                .Where(m => m.ControllerId != member.ControllerId)
                .Select(m => m.ControllerId)
                .OrderBy(id => id)
                .ToList();

            Assert.Equal(expected, context.ControllersInMission.OrderBy(id => id).ToList());
        }
    }
}
