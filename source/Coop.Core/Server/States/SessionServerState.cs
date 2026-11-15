using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Common.Services.Connection.Messages;
using Coop.Core.Server.Services.Instances.Sessions;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.UI.Interfaces;
using Serilog;
using System;
using System.Threading.Tasks;

namespace Coop.Core.Server.States;

/// <summary>
/// State of a server that hosts a campaign-free session: the network runs and clients join, but no campaign
/// is loaded and no registry or campaign service is touched. The process exits when the session closes.
/// Copy of <see cref="ServerRunningState"/> with the session close added.
/// </summary>
public class SessionServerState : ServerStateBase
{
    private static readonly ILogger Logger = LogManager.GetLogger<SessionServerState>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IGameStateInterface gameStateInterface;

    /// <summary>How long the exit waits after the close, so the end-of-mission messages reach every client first.</summary>
    internal TimeSpan QuitDelay { get; set; } = TimeSpan.FromSeconds(2);

    public SessionServerState(
        IServerLogic logic,
        IMessageBroker messageBroker,
        INetwork network,
        IGameStateInterface gameStateInterface,
        ILoadingInterface loadingInterface) : base(logic)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.gameStateInterface = gameStateInterface;

        network.Start();

        loadingInterface.HideLoadingScreen();

        messageBroker.Subscribe<MainMenuEntered>(Handle_MainMenuEntered);
        messageBroker.Subscribe<SessionClosed>(Handle_SessionClosed);
    }

    public override void Dispose()
    {
        messageBroker.Unsubscribe<MainMenuEntered>(Handle_MainMenuEntered);
        messageBroker.Unsubscribe<SessionClosed>(Handle_SessionClosed);
    }

    public override void Start()
    {
    }

    public override void Stop()
    {
        network.Dispose();
        gameStateInterface.GoToMainMenu();
    }

    internal void Handle_MainMenuEntered(MessagePayload<MainMenuEntered> payload)
    {
        messageBroker.Publish(this, new SendPopupMessage("Server has been stopped"));
        messageBroker.Publish(this, new EndCoopMode());

        Logic.SetState<InitialServerState>();
    }

    private void Handle_SessionClosed(MessagePayload<SessionClosed> payload)
    {
        Logger.Information("Session {Session} closed; the server process exits", payload.What.SessionId);
        Task.Delay(QuitDelay).ContinueWith(_ => gameStateInterface.QuitApplication());
    }
}
