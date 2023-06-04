using Common.Messaging;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.MobileParties.Messages;
using System;

namespace Coop.Core.Server.States;

public class ServerRunningState : ServerStateBase
{
    public ServerRunningState(IServerLogic logic, IMessageBroker messageBroker) : base(logic, messageBroker)
    {
        MessageBroker.Subscribe<MainMenuEntered>(Handle_MainMenuEntered);
        MessageBroker.Subscribe<MainPartyRemoved>(Handle_MainPartyRemoved);
    }

    public override void Dispose()
    {
        MessageBroker.Unsubscribe<MainPartyRemoved>(Handle_MainPartyRemoved);
        MessageBroker.Unsubscribe<MainMenuEntered>(Handle_MainMenuEntered);
    }

    public override void Start()
    {
    }

    public override void Stop()
    {
        // Stop server
        Logic.Network.Stop();

        // Go to main menu
        MessageBroker.Publish(this, new EnterMainMenu());
    }

    internal void Handle_MainMenuEntered(MessagePayload<MainMenuEntered> payload)
    {
        Logic.State = new InitialServerState(Logic, MessageBroker);
    }

    private void Handle_MainPartyRemoved(MessagePayload<MainPartyRemoved> obj)
    {
        MessageBroker.Publish(this, new ShowAllParties());
    }
}
