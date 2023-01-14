using Common.Messaging;
using GameInterface.Services.GameState.Messages;

namespace Coop.Core.Server.States
{
    public class ServerRunningState : ServerStateBase
    {
        public ServerRunningState(IServerLogic logic, IMessageBroker messageBroker) : base(logic, messageBroker)
        {
            MessageBroker.Subscribe<MainMenuEntered>(Handle_MainMenuEntered);
        }

        public override void Dispose()
        {
            MessageBroker.Unsubscribe<MainMenuEntered>(Handle_MainMenuEntered);
        }

        public override void Start()
        {
        }

        public override void Stop()
        {
            // Stop server
            Logic.NetworkServer.Stop();

            // Go to main menu
            MessageBroker.Publish(this, new EnterMainMenu());
        }

        private void Handle_MainMenuEntered(MessagePayload<MainMenuEntered> payload)
        {
            Logic.State = new InitialServerState(Logic, MessageBroker);
        }
    }
}
