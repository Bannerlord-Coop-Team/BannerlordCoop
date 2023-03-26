using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameInterface.Services.Heroes.Messages
{
    public readonly struct RegisterAllHeroes
    {
        public Guid TransactionID { get; }

        public RegisterAllHeroes(Guid transactionID)
        {
            TransactionID = transactionID;
        }
    }

    public readonly struct HeroesRegistered : IResponse
    {
        public Guid TransactionID { get; }

        public HeroesRegistered(Guid transactionID)
        {
            TransactionID = transactionID;
        }
    }
}
