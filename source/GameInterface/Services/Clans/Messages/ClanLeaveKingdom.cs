using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages
{
    public record ClanLeaveKingdom : IEvent
    {
        public Clan Clan { get; }
        public bool GiveBackFiefs { get; }

        public ClanLeaveKingdom(Clan clan, bool giveBackFiefs)
        {
            Clan = clan;
            GiveBackFiefs = giveBackFiefs;
        }
    }
}
