using Common.Messaging;
using Common.Network;
using Coop.Core.Common.Services.Connection.Messages;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.UI.Interfaces;

namespace Coop.Core.Server.States;

/// <summary>
/// State representing the server is in the campaign and running
/// </summary>
public class ServerRunningState : ServerStateBase
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IGameStateInterface gameStateInterface;

    public ServerRunningState(
        IServerLogic logic,
        IMessageBroker messageBroker,
        INetwork network,
        IGameStateInterface gameStateInterface,
        ILoadingInterface loadingInterface,
        ITimeControlInterface timeControlInterface) : base(logic)    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.gameStateInterface = gameStateInterface;

        // Start server
        network.Start();

        // A loaded dedicated campaign starts paused. Advancing it at 1x immediately lets Bannerlord
        // finish its first campaign tick and materialize the map before any player begins joining.
        timeControlInterface.ServerSetTimeControl(TimeControlEnum.Play_1x);

        loadingInterface.HideLoadingScreen();

        messageBroker.Subscribe<MainMenuEntered>(Handle_MainMenuEntered);
    }

    public override void Dispose()
    {
        messageBroker.Unsubscribe<MainMenuEntered>(Handle_MainMenuEntered);
    }

    public override void Start()
    {
    }

    public override void Stop()
    {
        // Stop server
        network.Dispose();

        // Go to main menu
        gameStateInterface.GoToMainMenu();
    }

    internal void Handle_MainMenuEntered(MessagePayload<MainMenuEntered> payload)
    {
        messageBroker.Publish(this, new SendPopupMessage("Server has been stopped"));
        messageBroker.Publish(this, new EndCoopMode());

        Logic.SetState<InitialServerState>();
    }
}
