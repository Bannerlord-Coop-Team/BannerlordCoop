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
        public ISet<Guid> ControlledHeros { get; }
        public IReadOnlyDictionary<string, Guid> PartyIds { get; }
        public IReadOnlyDictionary<string, Guid> HeroIds { get; }

        public ObjectGuidsPackaged(
            Guid transactionID,
            string uniqueGameId,
            ISet<Guid> controlledHeros,
            IReadOnlyDictionary<string, Guid> partyIds,
            IReadOnlyDictionary<string, Guid> heroIds)
        {
            TransactionID = transactionID;
            UniqueGameId = uniqueGameId;
            ControlledHeros = controlledHeros;
            PartyIds = partyIds;
            HeroIds = heroIds;
        }
    }
}
