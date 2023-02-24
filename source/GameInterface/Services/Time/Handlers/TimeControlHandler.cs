using Common.Messaging;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Time.Messages;

namespace GameInterface.Services.Heroes.Handlers
{
    internal class TimeControlHandler : IHandler
    {
        private readonly ITimeControlInterface timeControlInterface;
        private readonly IMessageBroker messageBroker;

        public TimeControlHandler(
            ITimeControlInterface timeControlInterface,
            IMessageBroker messageBroker)
        {
            this.timeControlInterface = timeControlInterface;
            this.messageBroker = messageBroker;

            messageBroker.Subscribe<PauseAndDisableGameTimeControls>(Handle);
            messageBroker.Subscribe<EnableGameTimeControls>(Handle);
        }

        private void Handle(MessagePayload<PauseAndDisableGameTimeControls> obj)
        {
            // TODO reenable
            //timeControlInterface.PauseAndDisableTimeControls();
        }

        private void Handle(MessagePayload<EnableGameTimeControls> obj)
        {
            timeControlInterface.EnableTimeControls();
        }
    }
}
