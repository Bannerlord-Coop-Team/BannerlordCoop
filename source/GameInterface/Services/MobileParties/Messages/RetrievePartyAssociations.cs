using Common.Messaging;
using System;

namespace GameInterface.Services.MobileParties.Messages
{
    /// <summary>
    /// Starts the packaging of all party string ids and the associated Guid
    /// </summary>
    public readonly struct RetrievePartyAssociations : ICommand
    {
        public Guid TransactionID { get; }

        public RetrievePartyAssociations(Guid transactionId)
        {
            TransactionID = transactionId;
        }
    }
}
