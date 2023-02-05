using Common.Messaging;

namespace GameInterface.Services.Heroes.Messages
{
    public readonly struct ResolveHero : ICommand
    {
        public int TransactionId { get; }
        public string PlayerId { get; }

        public ResolveHero(int transactionId, string playerId)
        {
            TransactionId = transactionId;
            PlayerId = playerId;
        }
    }
}