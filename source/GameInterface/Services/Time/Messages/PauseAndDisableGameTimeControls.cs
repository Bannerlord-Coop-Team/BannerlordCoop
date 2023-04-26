using Common.Messaging;
using System;

namespace GameInterface.Services.Time.Messages
{
    public readonly struct PauseAndDisableGameTimeControls : ICommand
    {
        public Guid TransactionID { get; }

        public PauseAndDisableGameTimeControls(Guid transactionID)
        {
            TransactionID = transactionID;
        }
    }
}
