using Common.Messaging;
using Common.PacketHandlers;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace Coop.Core.Client.Services.Clans.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public record NetworkClanLeaveKingdomRequest : ICommand
    {
        [ProtoMember(1)]
        public string ClanId { get; }
        [ProtoMember(2)]
        public bool GiveBackFiefs { get; }

        public NetworkClanLeaveKingdomRequest(string clan, bool giveBackFiefs)
        {
            ClanId = clan;
            GiveBackFiefs = giveBackFiefs;
        }
    }
}