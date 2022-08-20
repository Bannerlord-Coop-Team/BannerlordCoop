using Common.Messaging;
using GameInterface.Services.GameState.Messages;

namespace Coop.Core.Server.States
{
    public class ServerRunningState : ServerStateBase
    {
        public ServerRunningState(IServerLogic logic, IMessageBroker messageBroker) : base(logic, messageBroker)
        {
            MessageBroker.Subscribe<MainMenuEntered>(Handle);
        }

        public override void Dispose()
        {
            MessageBroker.Unsubscribe<MainMenuEntered>(Handle);
        }

        public override void Start()
        {
        }

        public override void Stop()
        {
            MessageBroker.Publish(this, new EnterMainMenu());
        }

        private void Handle(MessagePayload<MainMenuEntered> payload)
        {
            Logic.State = new InitialServerState(Logic, MessageBroker);
        }
    }
}
