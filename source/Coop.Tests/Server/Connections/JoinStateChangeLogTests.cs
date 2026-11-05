using Autofac;
using Common.Logging;
using Coop.Core.Client;
using Coop.Core.Server.Connections;
using Coop.Tests.Mocks;
using LiteNetLib;
using System;
using System.Collections.Concurrent;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using ClientCampaignState = Coop.Core.Client.States.CampaignState;
using ServerCampaignState = Coop.Core.Server.Connections.States.CampaignState;

namespace Coop.Tests.Server.Connections;

/// <summary>
/// The client and connection state changes are the only lines that show how far a join got, so they
/// must reach a Release log, whose floor is Information (the tests' default level too).
/// </summary>
public class JoinStateChangeLogTests : IDisposable
{
    private readonly ConcurrentQueue<string> logs = new ConcurrentQueue<string>();
    private readonly Action<string> captureLog;
    private readonly ITestOutputHelper output;

    public JoinStateChangeLogTests(ITestOutputHelper output)
    {
        this.output = output;
        captureLog = logs.Enqueue;
        OutputSinkManager.AddLogCallback(captureLog);
    }

    public void Dispose() => OutputSinkManager.RemoveLogCallback(captureLog);

    [Fact]
    public void ClientStateChange_IsLoggedAtInformation()
    {
        var logic = new ClientTestComponent(output).Container.Resolve<IClientLogic>();

        logic.SetState<ClientCampaignState>();

        Assert.Contains(logs, line => line.StartsWith("Client is changing to") && line.Contains("CampaignState"));
    }

    [Fact]
    public void ConnectionStateChange_IsLoggedAtInformation()
    {
        var container = new ServerTestComponent(output).Container;
        NetPeer peer = container.Resolve<TestNetwork>().CreatePeer();
        var logic = container.Resolve<IConnectionLogic>(new TypedParameter(typeof(NetPeer), peer));

        logic.SetState<ServerCampaignState>();

        Assert.Contains(logs, line => line.StartsWith("Connection is changing to") && line.Contains("CampaignState"));
    }
}
