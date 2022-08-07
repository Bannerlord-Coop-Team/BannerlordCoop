using Coop.Communication.MessageBroker;
using Coop.Mod.Messages.Game;

namespace Coop.Mod.LogicStates.Client
{
    internal class LoadingWorldState : ClientState
    {
        public LoadingWorldState(IMessageBroker messageBroker, IClientLogic logic) : base(messageBroker, logic) { }

        public override void Connect()
        {
            _messageBroker.Subscribe<GoToMainMenuResponse>(Handle_MainMenuTransition);
            _messageBroker.Publish(this, new GoToMainMenuRequest());
        }

        private void Handle_MainMenuTransition(MessagePayload<GoToMainMenuResponse> payload)
        {

            _messageBroker.Unsubscribe<GoToMainMenuResponse>(Handle_MainMenuTransition);
            var nextState = new InitialClientState(_messageBroker, _logic);
            _logic.State = nextState;
            nextState.Connect();
        }
    }
}
