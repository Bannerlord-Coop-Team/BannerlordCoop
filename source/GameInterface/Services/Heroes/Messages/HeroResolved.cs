using Common.Messaging;
using System;

namespace GameInterface.Services.Heroes.Messages
{
    public readonly struct HeroResolved : IEvent
    {
        public int TransactionId { get; }
        public uint HeroId { get; }

        public HeroResolved(int transactionId, uint heroId)
        {
            TransactionId = transactionId;
            HeroId = heroId;
        }
    }
}