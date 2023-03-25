using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameInterface.Services.Heroes.Messages
{
    internal readonly struct RegisterExistingControlledHeroes : ICommand
    {
        public Guid TransactionID { get; }
        public IEnumerable<Guid> ControlledHeroIds { get; }

        public RegisterExistingControlledHeroes(Guid transactionID, IEnumerable<Guid> controlledHeroIds)
        {
            TransactionID = transactionID;
            ControlledHeroIds = controlledHeroIds;
        }
    }
}
