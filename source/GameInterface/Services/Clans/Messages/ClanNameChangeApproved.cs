using Common.Messaging;
using Common.PacketHandlers;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public record ClanNameChangeApproved : ICommand
    {
        [ProtoMember(1)]
        public string ClanId { get; }
        [ProtoMember(2)]
        public string Name { get; }
        [ProtoMember(3)]
        public string InformalName { get; }

        public ClanNameChangeApproved(string clanId, string name, string informalName)
        {
            ClanId = clanId;
            Name = name;
            InformalName = informalName;
        }
    }
}