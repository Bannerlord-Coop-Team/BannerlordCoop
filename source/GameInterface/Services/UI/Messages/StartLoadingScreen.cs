using Common.Messaging;
using System;

namespace GameInterface.Services.UI.Messages
{
    public readonly struct StartLoadingScreen : ICommand
    {
        public Guid TransactionID => throw new NotImplementedException();
    }
}
