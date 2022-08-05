using Common.MessageBroker;
using Coop.Mod.Messages.Game;
using System;

namespace Coop.Mod.LogicStates.Client
{
    internal class LoadingWorldState : ClientState
    {
        IMessageBroker _messageBroker { get; }
        public LoadingWorldState(IClientLogic clientContext) : base(clientContext)
        {
            _messageBroker = clientContext.Communicator.MessageBroker;
        }

        public override void Connect()
        {
            _messageBroker.Subscribe<GoToMainMenuResponse>(Handle_MainMenuTransition);
            _messageBroker.Publish(this, new GoToMainMenuRequest());
        }

        private void Handle_MainMenuTransition(MessagePayload<GoToMainMenuResponse> payload)
        {

            _messageBroker.Unsubscribe<GoToMainMenuResponse>(Handle_MainMenuTransition);
            var nextState = new InitialClientState(_clientContext);
            _clientContext.State = nextState;
            nextState.Connect();
        }
    }
}
