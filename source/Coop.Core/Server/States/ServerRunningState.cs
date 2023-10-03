using Common.Messaging;
using Common.Network;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.MobileParties.Messages;
using HarmonyLib;

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
        network.Stop();

        // Go to main menu
        messageBroker.Publish(this, new EnterMainMenu());
    }

    internal void Handle_MainMenuEntered(MessagePayload<MainMenuEntered> payload)
    {
        Logic.SetState<InitialServerState>();
    }
}
