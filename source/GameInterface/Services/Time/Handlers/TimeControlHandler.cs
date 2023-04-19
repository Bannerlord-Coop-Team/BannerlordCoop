using Common.Messaging;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Time.Messages;
using TaleWorlds.CampaignSystem;

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
            messageBroker.Subscribe<SetTimeControlMode>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<PauseAndDisableGameTimeControls>(Handle);
            messageBroker.Unsubscribe<EnableGameTimeControls>(Handle);
            messageBroker.Unsubscribe<SetTimeControlMode>(Handle);
        }

        private void Handle(MessagePayload<PauseAndDisableGameTimeControls> obj)
        {
            timeControlInterface.PauseAndDisableTimeControls();
        }

        private void Handle(MessagePayload<EnableGameTimeControls> obj)
        {
            timeControlInterface.EnableTimeControls();
        }

        private void Handle(MessagePayload<SetTimeControlMode> obj)
        {
            var payload = obj.What;
            CampaignTimeControlMode newTimeMode = (CampaignTimeControlMode)payload.NewTimeMode;
            timeControlInterface.SetTimeControl(newTimeMode);

            messageBroker.Publish(this, new TimeControlModeSet(payload.TransactionID, payload.NewTimeMode));
        }
    }
}
