using Common.Messaging;
using System;

namespace GameInterface.Services.Heroes.Messages
{
    public readonly struct HeroResolved : IEvent
    {
        public int TransactionId { get; }
        public string HeroStringId { get; }

        public HeroResolved(int transactionId, string heroStringId)
        {
            TransactionId = transactionId;
            HeroStringId = heroStringId;
        }
    }
}