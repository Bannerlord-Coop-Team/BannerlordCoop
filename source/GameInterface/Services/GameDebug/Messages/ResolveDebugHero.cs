using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameInterface.Services.GameDebug.Messages
{
    public readonly struct ResolveDebugHero
    {
        public int TransactionId { get; }
        public string PlayerId { get; }

        public ResolveDebugHero(int transactionId, string playerId)
        {
            TransactionId = transactionId;
            PlayerId = playerId;
        }
    }
}
