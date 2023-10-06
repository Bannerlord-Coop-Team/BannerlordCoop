using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages
{
    /// <summary>
    /// Local event when a clan name is updated from game interface
    /// </summary>
    public record ClanNameChange : IEvent
    {
        public string ClanId { get; }
        public string Name { get; }
        public string InformalName { get; }

        public ClanNameChange(string clanId, string name, string informalName)
        {
            ClanId = clanId;
            Name = name;
            InformalName = informalName;
        }
    }
}
