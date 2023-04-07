using Common.Messaging;
using System;

namespace GameInterface.Services.Heroes.Messages
{
    public readonly struct ResolveHero : ICommand
    {
        public Guid TransactionID { get; }
        public string PlayerId { get; }

        public ResolveHero(Guid transactionId, string playerId)
        {
            TransactionID = transactionId;
            PlayerId = playerId;
        }
    }
}