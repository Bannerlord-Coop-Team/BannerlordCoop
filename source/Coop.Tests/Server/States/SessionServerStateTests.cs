using Autofac;
using Common.Messaging;
using Common.Network;
using Coop.Core.Common.Services.Connection.Messages;
using Coop.Core.Server;
using Coop.Core.Server.Services.Instances.Sessions;
using Coop.Core.Server.States;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.UI.Interfaces;
using Moq;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.States;

public class SessionServerStateTests
{
    private readonly IServerLogic serverLogic;
    private readonly ServerTestComponent serverComponent;

    public SessionServerStateTests(ITestOutputHelper output)
    {
        serverComponent = new ServerTestComponent(output);
        serverLogic = serverComponent.Container.Resolve<IServerLogic>();
    }

    [Fact]
    public void MainMenuEntered_DoesNotEndTheSession()
    {
        // A screen closing over the initial state raises MainMenuEntered on a session server.
        serverLogic.SetState<SessionServerState>();

        serverComponent.TestMessageBroker.Publish(this, new MainMenuEntered());

        Assert.IsType<SessionServerState>(serverLogic.State);
        Assert.Empty(serverComponent.TestMessageBroker.GetMessagesFromType<EndCoopMode>());
    }

    [Fact]
    public void SessionClosed_QuitsTheApplication()
    {
        var gameState = new Mock<IGameStateInterface>();
        using var state = new SessionServerState(serverLogic, serverComponent.TestMessageBroker, new Mock<INetwork>().Object,
            gameState.Object, new Mock<ILoadingInterface>().Object) { QuitDelay = TimeSpan.Zero };

        serverComponent.TestMessageBroker.Publish(this, new SessionClosed("session:abc"));

        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < TimeSpan.FromSeconds(5) && !gameState.Invocations.Any(call => call.Method.Name == nameof(IGameStateInterface.QuitApplication)))
            Thread.Sleep(10);
        gameState.Verify(x => x.QuitApplication(), Times.Once);
    }
}
