using Common.Messaging;
using Common.PacketHandlers;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace Coop.Core.Client.Services.Clans.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public record ClanLeftKingdom : IEvent
    {
        public string ClanId { get; }
        public bool GiveBackFiefs { get; }

        public ClanLeftKingdom(string clan, bool giveBackFiefs)
        {
            ClanId = clan;
            GiveBackFiefs = giveBackFiefs;
        }
    }
}