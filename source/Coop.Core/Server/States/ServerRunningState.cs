using Common.Messaging;
using Common.Network;
using Coop.Core.Common.Services.Connection.Messages;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.GameState.Messages;

namespace Coop.Core.Server.States;

/// <summary>
/// State representing the server is in the campaign and running
/// </summary>
public class ServerRunningState : ServerStateBase
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;

    public ServerRunningState(IServerLogic logic, IMessageBroker messageBroker, INetwork network) : base(logic)
    {
        this.messageBroker = messageBroker;
        this.network = network;

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
        messageBroker.Publish(this, new EnterMainMenu());
    }

    internal void Handle_MainMenuEntered(MessagePayload<MainMenuEntered> payload)
    {
        messageBroker.Publish(this, new SendPopupMessage("Server has been stopped"));
        messageBroker.Publish(this, new EndCoopMode());

        Logic.SetState<InitialServerState>();
    }
}
