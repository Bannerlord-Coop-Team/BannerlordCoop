using Common.Messaging;
using GameInterface.Services.Heroes.Handlers;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Modules.Interfaces;
using GameInterface.Services.Modules.Messages;
using System;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Modules.Handlers
{
    internal class ModuleHandler : IHandler
    {
        private readonly IModuleInterface moduleInterface;
        private readonly IMessageBroker messageBroker;

        public ModuleHandler(
            IModuleInterface moduleInterface,
            IMessageBroker messageBroker)
        {
            this.moduleInterface = moduleInterface;
            this.messageBroker = messageBroker;

            messageBroker.Subscribe<ValidateModules>(Handle);
        }

        private void Handle(MessagePayload<ValidateModules> obj)
        {
            moduleInterface.ValidateModules();
        }
    }
}
