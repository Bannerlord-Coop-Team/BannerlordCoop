using Common.Messaging;
using System;

namespace GameInterface.Services.Heroes.Messages
{
    public readonly struct HeroResolved : IResponse
    {
        public Guid TransactionID { get; }
        public string HeroId { get; }
        public HeroResolved(Guid transactionID, string heroId)
        {
            TransactionID = transactionID;
            HeroId = heroId;
        }
    }
}