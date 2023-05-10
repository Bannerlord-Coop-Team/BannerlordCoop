using Common.Messaging;
using GameInterface.Services.Modules.Interfaces;
using GameInterface.Services.Modules.Messages;

namespace GameInterface.Services.Modules.Handlers;

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

    public void Dispose()
    {
        messageBroker.Unsubscribe<ValidateModules>(Handle);
    }

    private void Handle(MessagePayload<ValidateModules> obj)
    {
        moduleInterface.ValidateModules();
    }
}
