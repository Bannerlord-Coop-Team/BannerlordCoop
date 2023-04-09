using Common.Messaging;
using GameInterface.Services.Modules.Messages;

namespace GameInterface.Services.Modules.Interfaces
{
    internal interface IModuleInterface : IGameAbstraction
    {
        void ValidateModules();
    }

    internal class ModuleInterface : IModuleInterface
    {
        private readonly IMessageBroker messageBroker;

        public ModuleInterface(IMessageBroker messageBroker)
        {
            this.messageBroker = messageBroker;
        }

        public void ValidateModules()
        {
            // TODO implement
            messageBroker.Publish(this, new ModulesProcessed());
        }
    }
}
