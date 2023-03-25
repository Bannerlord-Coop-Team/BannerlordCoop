using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameInterface.Services.Save.Messages
{
    public readonly struct PackageObjectGuids : ICommand
    {
        public Guid TransactionID { get; }

        public PackageObjectGuids(Guid transactionID)
        {
            TransactionID = transactionID;
        }
    }

    public readonly struct ObjectGuidsPackaged : IResponse
    {
        public Guid TransactionID { get; }
        public string UniqueGameId { get; }
        public HashSet<Guid> ControlledHeros { get; }
        public Dictionary<string, Guid> PartyIds { get; }
        public Dictionary<string, Guid> HeroIds { get; }

        public ObjectGuidsPackaged(
            Guid transactionID,
            string uniqueGameId,
            HashSet<Guid> controlledHeros,
            Dictionary<string, Guid> partyIds,
            Dictionary<string, Guid> heroIds)
        {
            TransactionID = transactionID;
            UniqueGameId = uniqueGameId;
            ControlledHeros = controlledHeros;
            PartyIds = partyIds;
            HeroIds = heroIds;
        }
    }
}
