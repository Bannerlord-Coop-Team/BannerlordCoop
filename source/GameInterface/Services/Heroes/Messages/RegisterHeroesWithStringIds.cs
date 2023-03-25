using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameInterface.Services.Heroes.Messages
{
    internal readonly struct RegisterHeroesWithStringIds
    {
        public Guid TransactionID { get; }
        public IReadOnlyDictionary<string, Guid> AssociatedStringIdValues { get; }

        public RegisterHeroesWithStringIds(
            Guid transactionID, 
            IReadOnlyDictionary<string, Guid> associatedStringIdValues)
        {
            TransactionID = transactionID;
            AssociatedStringIdValues = associatedStringIdValues;
        }
    }
}
