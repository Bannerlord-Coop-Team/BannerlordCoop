using Common.Messaging;
using System;

namespace GameInterface.Services.Heroes.Messages
{
    public readonly struct HeroResolved : IResponse
    {
        public Guid TransactionID { get; }
        public Guid HeroId { get; }
        public HeroResolved(Guid transactionID, Guid heroId)
        {
            TransactionID = transactionID;
            HeroId = heroId;
        }
    }
}