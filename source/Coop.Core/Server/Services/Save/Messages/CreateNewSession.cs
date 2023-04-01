using Common.Messaging;
using System;

namespace Coop.Core.Server.Services.Save.Handlers
{
    public readonly struct CreateNewSession : ICommand
    {
        public Guid TransactionID { get; }

        public CreateNewSession(Guid transactionID)
        {
            TransactionID = transactionID;
        }
    }
}