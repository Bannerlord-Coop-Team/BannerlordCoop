using Common.Messaging;
using System;

namespace GameInterface.Services.Modules.Messages
{
    public readonly struct ValidateModules : ICommand
    {
        public Guid TransactionID => throw new NotImplementedException();
    }

    public readonly struct ModulesProcessed : IEvent
    {
        public ModuleInfo[] Modules { get; }
    }
}
