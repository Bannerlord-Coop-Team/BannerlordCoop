using Coop.Core.Server.Services.Instances;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Entity;
using GameInterface.Services.Missions;
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
    }

    [Fact]
    public void ClientLeaves_DepartureMarksWhetherInstanceIsEmpty()
    {
        var members = SetupClients().Take(2).ToArray();
        var departures = new List<MissionMemberDeparted>();
        TestEnvironment.Server.Subscribe<MissionMemberDeparted>(payload => departures.Add(payload.What));

        Join(members[0]);
        Join(members[1]);

        var membershipRegistry = TestEnvironment.Server.Resolve<IMissionMembershipRegistry>();
        Assert.True(membershipRegistry.IsInstanceOccupied(InstanceId));

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
        Assert.False(membershipRegistry.IsInstanceOccupied(InstanceId));
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

    /// <summary>Simulates the server receiving a MissionEntered over the member's connection.</summary>
    private void Join(Member member) =>
        TestEnvironment.Server.SimulateMessage(member.Instance.NetPeer, new NetworkMissionEntered(member.ControllerId, InstanceId));

    /// <summary>Simulates the server receiving a MissionLeft over the member's connection.</summary>
    private void Leave(Member member) =>
        TestEnvironment.Server.SimulateMessage(member.Instance.NetPeer, new NetworkMissionLeft(member.ControllerId, InstanceId));

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
