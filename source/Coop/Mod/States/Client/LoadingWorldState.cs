using Common.MessageBroker;
using Coop.Mod.Messages.Game;
using System;

namespace Coop.Mod.States.Client
{
    internal class LoadingWorldState : ClientState
    {
        public LoadingWorldState(IClientContext clientContext) : base(clientContext)
        {
        }

        public override void Connect()
        {
            _clientContext.MessageBroker.Subscribe<GoToMainMenuResponse>(Handle_MainMenuTransition);
            _clientContext.MessageBroker.Publish(this, new GoToMainMenuRequest());
        }

        private void Handle_MainMenuTransition(MessagePayload<GoToMainMenuResponse> payload)
        {
            
            _clientContext.MessageBroker.Unsubscribe<GoToMainMenuResponse>(Handle_MainMenuTransition);
            var nextState = new InitialClientState(_clientContext);
            _clientContext.State = nextState;
            nextState.Connect();
        }
    }
}
