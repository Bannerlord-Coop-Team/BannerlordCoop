using Common.Network;
using Coop.Core.Server.Services.Instances.Sessions;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.Missions;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.MapEvents;
using Missions.Messages;
using Missions.Sessions.Messages;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Sessions;

/// <summary>
/// E2E environment for campaign-free sessions: the server opens a session definition, and clients join it as
/// players with no hero or party, the way a session server registers them.
/// </summary>
public class SessionTestEnvironment : MissionTestEnvironment
{
    public SessionTestEnvironment(ITestOutputHelper output, int numClients = 3) : base(output, numClients)
    {
    }

    /// <summary>Opens a session definition on the server and returns its id.</summary>
    protected string OpenSession(string modId = "test.mod", byte[]? payload = null)
    {
        string sessionId = null!;
        Server.Call(() => sessionId = Server.Resolve<ISessionDefinitionStore>().Open(modId, payload ?? new byte[] { 1, 2, 3 }).SessionId);
        return sessionId;
    }

    /// <summary>
    /// Registers <paramref name="controllerId"/> as a null-id session player on the server, binds its peer, gives the
    /// client that controller id and announces the mission entry the way a client mission does.
    /// </summary>
    protected void JoinSession(EnvironmentInstance client, string controllerId, string sessionId)
    {
        SetControllerId(client, controllerId);
        Server.Call(() =>
        {
            var players = Server.Resolve<IPlayerManager>();
            if (!players.TryGetPlayer(controllerId, out _))
                players.AddPlayer(new Player(controllerId, null, null, null, null));
            players.SetPeer(controllerId, client.NetPeer);
        });
        Server.SimulateMessage(client.NetPeer, new NetworkMissionEntered(controllerId, sessionId));
    }

    /// <summary>Simulates the client finishing its mission load: it asks the server for the session host.</summary>
    protected void RequestSessionHost(EnvironmentInstance client, string sessionId)
    {
        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkRequestSessionHost(sessionId)));
    }

    protected void RequestSessionClose(EnvironmentInstance client, string sessionId, int epoch)
    {
        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkRequestSessionClose(sessionId, epoch)));
    }

    /// <summary>The client announces it left the session mission; the server membership handler publishes the departure.</summary>
    protected void LeaveSession(EnvironmentInstance client, string controllerId, string sessionId)
    {
        Server.SimulateMessage(client.NetPeer, new NetworkMissionLeft(controllerId, sessionId));
    }

    protected void AssertSessionHost(EnvironmentInstance instance, string sessionId, string expectedHost, int expectedEpoch, params string[] expectedSuccessors)
    {
        instance.Call(() =>
        {
            var registry = instance.Resolve<IBattleHostRegistry>();
            Assert.True(registry.TryGet(sessionId, out var assignment), $"No host assignment on {instance.GetType().Name}");
            Assert.Equal(expectedHost, assignment.HostControllerId);
            Assert.Equal(expectedEpoch, assignment.Epoch);
            Assert.Equal(expectedSuccessors, assignment.SuccessorControllerIds.ToArray());
        });
    }
}
