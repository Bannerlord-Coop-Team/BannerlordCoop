using Common.Messaging;
using GameInterface.Services.CharacterCreation.Interfaces;
using GameInterface.Services.CharacterCreation.Messages;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Time.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        }

        private void Handle(MessagePayload<PauseAndDisableGameTimeControls> obj)
        {
            timeControlInterface.PauseAndDisableTimeControls();
        }

        private void Handle(MessagePayload<EnableGameTimeControls> obj)
        {
            timeControlInterface.EnableTimeControls();
        }
    }
}
