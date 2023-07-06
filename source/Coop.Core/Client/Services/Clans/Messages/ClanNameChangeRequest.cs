using Common.Messaging;
using Common.PacketHandlers;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace Coop.Core.Client.Services.Clans.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public record ClanNameChangeRequest : ICommand
    {
        [ProtoMember(1)]
        public string ClanId { get; }
        [ProtoMember(2)]
        public string Name { get; }
        [ProtoMember(3)]
        public string InformalName { get; }

        public ClanNameChangeRequest(string clan, string name, string informalName)
        {
            ClanId = clan;
            Name = name;
            InformalName = informalName;
        }
    }
}