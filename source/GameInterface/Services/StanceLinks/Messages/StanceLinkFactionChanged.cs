using System;
using System.Collections.Generic;
using System.Text;
using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.StanceLinks.Messages.Data
{
    /// <summary>
    /// Command to update a faction inside a StanceLink on client side.
    /// </summary>
    public record StanceLinkFactionChanged : IEvent
    {
        public StanceLinkFactionChanged(StanceLink _StanceLink, IFaction _Faction, bool _isFaction1)
        {
            StanceLink = _StanceLink;
            Faction = _Faction;
            IsFaction1 = _isFaction1;
        }

        public StanceLink StanceLink { get; }
        public IFaction Faction { get; }
        public bool IsFaction1 { get; }
    }
}
