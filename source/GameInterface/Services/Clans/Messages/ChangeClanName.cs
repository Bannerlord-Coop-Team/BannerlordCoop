using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages
{
    /// <summary>
    /// Event to update game interface when clan name is changed
    /// </summary>
    public record ChangeClanName : ICommand
    {
        public string ClanId { get; }
        public string Name { get; }
        public string InformalName { get; }

        public ChangeClanName(string clanId, string name, string informalName)
        {
            ClanId = clanId;
            Name = name;
            InformalName = informalName;
        }
    }
}
