using Common.Messaging;
using System;
using System.Collections.Generic;

namespace GameInterface.Services.Save.Messages
{
    public readonly struct LoadExistingObjectGuids : ICommand
    {
        public Guid TransactionID { get; }
        public IReadOnlyCollection<Guid> ControlledHeros { get; }
        public IReadOnlyDictionary<string, Guid> PartyIds { get; }
        public IReadOnlyDictionary<string, Guid> HeroIds { get; }

        public LoadExistingObjectGuids(
            Guid transactionID,
            IReadOnlyCollection<Guid> controlledHeros,
            IReadOnlyDictionary<string, Guid> partyIds,
            IReadOnlyDictionary<string, Guid> heroIds)
        {
            TransactionID = transactionID;
            ControlledHeros = controlledHeros;
            PartyIds = partyIds;
            HeroIds = heroIds;
        }
    }

    public readonly struct ExistingObjectGuidsLoaded : IResponse
    {
        public Guid TransactionID { get; }

        public ExistingObjectGuidsLoaded(Guid transactionID)
        {
            TransactionID = transactionID;
        }
    }
}
