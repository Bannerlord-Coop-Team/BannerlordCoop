using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameInterface.Services.Heroes.Messages
{
    /// <summary>
    /// Command to start packaging heroes, <seealso cref="HeroAssociationsPackaged"/>
    /// </summary>
    public readonly struct RetrieveHeroAssociations : ICommand
    {
        public Guid TransactionID { get; }

        public RetrieveHeroAssociations(Guid transactionID)
        {
            TransactionID = transactionID;
        }
    }
}
