using Common.Messaging;
using System;

namespace GameInterface.Services.MobileParties.Messages
{

    /// <summary>
    /// Registers all existing parties with new Guids
    /// </summary>
    public readonly struct RegisterAllParties : ICommand
    {
        public Guid TransactionID { get; }
        public RegisterAllParties(Guid transactionID)
        {
            TransactionID = transactionID;
        }
    }

    public readonly struct PartiesRegistered : IResponse
    {
        public Guid TransactionID { get; }

        public PartiesRegistered(Guid transactionId)
        {
            TransactionID = transactionId;
        }
    }
}
