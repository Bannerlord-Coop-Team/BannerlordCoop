using Common.Messaging;
using System;
using System.Collections.Generic;

namespace GameInterface.Services.MobileParties.Messages
{
    public readonly struct RegisterPartiesWithStringIds : ICommand
    {
        public Guid TransactionID { get; }
        public IReadOnlyDictionary<string, Guid> AssociatedStringIdValues { get; }

        public RegisterPartiesWithStringIds(Guid transactionID, IReadOnlyDictionary<string, Guid> associatedStringIdValues)
        {
            TransactionID = transactionID;
            AssociatedStringIdValues = associatedStringIdValues;
        }
    }
}