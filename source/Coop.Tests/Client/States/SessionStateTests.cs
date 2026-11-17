using Autofac;
using Common;
using Common.Messaging;
using Coop.Core.Client;
using Coop.Core.Client.States;
using Coop.Core.Common;
using GameInterface.Registry;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.MapEvents;
using GameInterface.Services.Sessions.Messages;
using GameInterface.Services.UI.Interfaces;
using Moq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
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

    // At the main menu no Game exists yet, so the items are registered by the session mission, not here.
    [Fact]
    public void SessionState_OpensTheMission_WithoutTouchingTheRegistries()
    {
        var registryManager = clientComponent.Container.Resolve<Mock<IRegistryManager>>();

        clientLogic.SetState<SessionState>();
        GameThread.Run(() => { }, blocking: true);

        Assert.Single(clientComponent.TestMessageBroker.GetMessagesFromType<SessionMissionOpenRequested>());
        registryManager.Verify(x => x.RegisterMissionItemObjects(), Times.Never);
        registryManager.Verify(x => x.RegisterAllGameObjects(), Times.Never);
    }

    [Fact]
    public void SessionState_HidesTheLoadingScreenOnTheGameThread()
    {
        var loading = clientComponent.Container.Resolve<Mock<ILoadingInterface>>();

        clientLogic.SetState<SessionState>();
        loading.Verify(x => x.HideLoadingScreen(), Times.Never);

        GameThread.Run(() => { }, blocking: true);
        loading.Verify(x => x.HideLoadingScreen(), Times.Once);
    }

    [Fact]
    public void Disconnect_AsksTheModToEndItsMission()
    {
        clientLogic.SetState<SessionState>();
        GameThread.Run(() => { }, blocking: true);

        clientLogic.Disconnect();
        GameThread.Run(() => { }, blocking: true);

        var ended = Assert.Single(clientComponent.TestMessageBroker.GetMessagesFromType<SessionMissionEndRequested>());
        Assert.Equal("session:abc", ended.SessionId);
    }

    [Fact]
    public void Disconnect_AtTheMainMenu_GoesToMainMenuState()
    {
        var logic = new Mock<IClientLogic>();
        logic.SetupProperty(x => x.Session, clientLogic.Session);
        var state = new SessionState(logic.Object, clientComponent.TestMessageBroker,
            Mock.Of<ILoadingInterface>(), Mock.Of<ICoopFinalizer>());
        GameThread.Run(() => { }, blocking: true);
        var manager = new GameStateManager(Mock.Of<IGameStateManagerOwner>(), GameStateManager.GameStateManagerType.Game);
        GameStateManager.Current = manager;
        try
        {
            manager.PushState(new InitialState());

            state.Disconnect();
            GameThread.Run(() => { }, blocking: true);
        }
        finally
        {
            GameStateManager.Current = null;
        }

        logic.Verify(x => x.SetState<MainMenuState>(), Times.Once);
        Assert.Empty(clientComponent.TestMessageBroker.GetMessagesFromType<SessionMissionEndRequested>());
    }

    [Fact]
    public void SessionState_MainMenu_Finalizes()
    {
        var finalizer = new Mock<ICoopFinalizer>();
        var logic = new Mock<IClientLogic>();
        logic.SetupProperty(x => x.Session, clientLogic.Session);
        var state = new SessionState(logic.Object, clientComponent.TestMessageBroker,
            Mock.Of<ILoadingInterface>(), finalizer.Object);
        BattleSpawnGate.BeginBattle("session:abc");

        state.Handle_MainMenuEntered(new MessagePayload<MainMenuEntered>(this, new MainMenuEntered()));

        Assert.False(BattleSpawnGate.IsCoopBattleActive);
        finalizer.Verify(x => x.Finalize("Left the co-op session"), Times.Once);
        logic.Verify(x => x.SetState<MainMenuState>(), Times.Once);
    }
}
