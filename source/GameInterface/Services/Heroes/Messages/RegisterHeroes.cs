using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameInterface.Services.Heroes.Messages
{
    public readonly struct RegisterHeroes
    {
        public Guid TransactionID { get; }

        public RegisterHeroes(Guid transactionID)
        {
            TransactionID = transactionID;
        }
    }
}
