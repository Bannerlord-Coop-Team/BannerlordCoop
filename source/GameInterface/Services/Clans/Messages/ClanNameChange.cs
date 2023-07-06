using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public record ClanNameChange : IEvent
    {
        [ProtoMember(1)]
        public Clan Clan { get; }
        [ProtoMember(2)]
        public string Name { get; }
        [ProtoMember(3)]
        public string InformalName { get; }

        public ClanNameChange(Clan clan, string name, string informalName)
        {
            Clan = clan;
            Name = name;
            InformalName = informalName;
        }
    }
}
