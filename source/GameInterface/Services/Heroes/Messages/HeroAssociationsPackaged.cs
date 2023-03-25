using Common.Messaging;
using System;
using System.Collections.Generic;

namespace GameInterface.Services.Heroes.Messages
{
    /// <summary>
    /// Packaged hero id and hero string id assosiations
    /// </summary>
    public readonly struct HeroAssociationsPackaged : IResponse
    {
        public Guid TransactionID { get; }
        public Dictionary<string, Guid> GuidToHeroStringId { get; }

        public HeroAssociationsPackaged(Guid transactionId, Dictionary<string, Guid> guidToHeroStringId)
        {
            TransactionID = transactionId;
            GuidToHeroStringId = guidToHeroStringId;
        }
    }
}
