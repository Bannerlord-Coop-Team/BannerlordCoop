using Common.Messaging;
using System;

namespace GameInterface.Services.MobileParties.Messages
{

    /// <summary>
    /// Registers all existing parties with new Guids
    /// </summary>
    public readonly struct RegisterParties : ICommand
    {
        public Guid TransactionID => throw new NotImplementedException();
    }
}
