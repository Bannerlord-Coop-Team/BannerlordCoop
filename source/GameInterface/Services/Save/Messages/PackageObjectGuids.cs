using Common.Messaging;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

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
        public string CampaignID { get; }
        public HashSet<Guid> ControlledHeros { get; }
        public Dictionary<string, Guid> PartyIds { get; }
        public Dictionary<string, Guid> HeroIds { get; }

        public ObjectGuidsPackaged(
            Guid transactionID,
            string campaignID,
            HashSet<Guid> controlledHeros, 
            Dictionary<string, Guid> partyIds, 
            Dictionary<string, Guid> heroIds)
        {
            TransactionID = transactionID;
            CampaignID = campaignID;
            ControlledHeros = controlledHeros;
            PartyIds = partyIds;
            HeroIds = heroIds;
        }
    }
}
