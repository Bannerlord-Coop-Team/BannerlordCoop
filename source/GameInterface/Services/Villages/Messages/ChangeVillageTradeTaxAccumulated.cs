using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Villages.Messages
{
    public record ChangeVillageTradeTaxAccumulated : ICommand
    {
        public string VillageId { get; }

        public int TradeTaxAccumulated { get; }

        public ChangeVillageTradeTaxAccumulated(string villageId, int tradeTaxAccumulated)
        {
            VillageId = villageId;
            TradeTaxAccumulated = tradeTaxAccumulated;
        }
    }
}
