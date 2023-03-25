using Common.Messaging;
using System;

namespace GameInterface.Services.Time.Messages
{
    public readonly struct PauseAndDisableGameTimeControls : ICommand
    {
        public Guid TransactionID => throw new NotImplementedException();
    }
}
