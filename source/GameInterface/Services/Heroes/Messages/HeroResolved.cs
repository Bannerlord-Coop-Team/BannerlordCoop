using Common.Messaging;
using System;

namespace GameInterface.Services.Heroes.Messages
{
    public readonly struct HeroResolved : IResponse
    {
        public Guid TransactionID { get; }
        public string HeroStringId { get; }
        public HeroResolved(Guid transactionID, string heroStringId)
        {
            TransactionID = transactionID;
            HeroStringId = heroStringId;
        }
    }
}