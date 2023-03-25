using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameInterface.Services.Heroes.Messages
{
    public readonly struct RetrieveControlledHeroes : ICommand
    {
        public Guid TransactionID { get; }

        public RetrieveControlledHeroes(Guid tansactionID)
        {
            TransactionID = tansactionID;
        }
    }
}
