
using Common.Messaging;
using GameInterface.Services.GameDebug.Messages;

namespace Coop.Core.Server.States
{
    public class InitialServerState : ServerStateBase
    {
        public InitialServerState(IServerLogic context, IMessageBroker messageBroker) : base(context, messageBroker)
        {
            MessageBroker.Subscribe<DebugGameStarted>(Handle);
        }

        public override void Dispose()
        {
            MessageBroker.Unsubscribe<DebugGameStarted>(Handle);
        }

        private void Handle(MessagePayload<DebugGameStarted> payload)
        {
            Logic.State = new ServerRunningState(Logic, MessageBroker);
        }

        public override void Start()
        {
            // TODO use UI screen
            MessageBroker.Publish(this, new LoadDebugGame());
        }

        public override void Stop()
        {

        }
    }
}
