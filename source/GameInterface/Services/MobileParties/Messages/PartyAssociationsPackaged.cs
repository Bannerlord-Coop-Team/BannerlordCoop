using Common.Messaging;
using System;
using System.Collections.Generic;

namespace GameInterface.Services.MobileParties.Messages
{
    /// <summary>
    /// Party string ids and associated Guids have been packaged
    /// </summary>
    public readonly struct PartyAssociationsPackaged : IResponse
    {
        public Guid TransactionID { get; }
        public Dictionary<string, Guid> AssociatedStringIdValues { get; }

        public PartyAssociationsPackaged(Guid transactionId,
                                         Dictionary<string, Guid> associatedStringIdValues)
        {
            TransactionID = transactionId;
            AssociatedStringIdValues = associatedStringIdValues;
        }
    }
}
