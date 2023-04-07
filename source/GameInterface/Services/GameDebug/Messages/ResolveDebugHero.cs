using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameInterface.Services.GameDebug.Messages
{
    public readonly struct ResolveDebugHero : ICommand
    {
        public Guid TransactionID { get; }
        public string PlayerId { get; }

        public ResolveDebugHero(Guid transactionId, string playerId)
        {
            TransactionID = transactionId;
            PlayerId = playerId;
        }
    }
}
