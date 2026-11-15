using Autofac;
using Common;
using Common.Messaging;
using Coop.Core.Client;
using Coop.Core.Client.States;
using Coop.Core.Common;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.MapEvents;
using GameInterface.Services.Sessions.Messages;
using GameInterface.Services.UI.Interfaces;
using Moq;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Client.States;

public class SessionStateTests
{
    private readonly ClientTestComponent clientComponent;
    private readonly IClientLogic clientLogic;

    public SessionStateTests(ITestOutputHelper output)
    {
        clientComponent = new ClientTestComponent(output);
        clientLogic = clientComponent.Container.Resolve<IClientLogic>();
        clientLogic.Session = new JoinedSessionInfo("session:abc", "test.mod", new byte[] { 4, 5, 6 });
    }

    [Fact]
    public void SessionState_PublishesOpenRequested()
    {
        clientLogic.SetState<SessionState>();
        GameThread.Run(() => { }, blocking: true);

        var requested = Assert.Single(clientComponent.TestMessageBroker.GetMessagesFromType<SessionMissionOpenRequested>());
        Assert.Equal("session:abc", requested.SessionId);
        Assert.Equal("test.mod", requested.ModId);
        Assert.Equal(new byte[] { 4, 5, 6 }, requested.Payload);
        clientComponent.Container.Resolve<Mock<ILoadingInterface>>().Verify(x => x.HideLoadingScreen(), Times.AtLeastOnce);
    }

    [Fact]
    public void SessionState_MainMenu_Finalizes()
    {
        var finalizer = new Mock<ICoopFinalizer>();
        var logic = new Mock<IClientLogic>();
        logic.SetupProperty(x => x.Session, clientLogic.Session);
        var state = new SessionState(logic.Object, clientComponent.TestMessageBroker,
            Mock.Of<IGameStateInterface>(), Mock.Of<ILoadingInterface>(), finalizer.Object);
        BattleSpawnGate.BeginBattle("session:abc");

        state.Handle_MainMenuEntered(new MessagePayload<MainMenuEntered>(this, new MainMenuEntered()));

        Assert.False(BattleSpawnGate.IsCoopBattleActive);
        finalizer.Verify(x => x.Finalize("Left the co-op session"), Times.Once);
        logic.Verify(x => x.SetState<MainMenuState>(), Times.Once);
    }

    [Fact]
    public void SessionState_IsRunningState()
    {
        Assert.False(clientLogic.RunningState);

        clientLogic.SetState<SessionState>();

        Assert.True(clientLogic.RunningState);
    }
}
