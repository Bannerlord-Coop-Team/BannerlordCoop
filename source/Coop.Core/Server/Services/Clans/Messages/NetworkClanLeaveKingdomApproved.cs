using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Clans.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public record NetworkClanLeaveKingdomApproved : ICommand
    {
        [ProtoMember(1)]
        public string ClanId { get; }
        [ProtoMember(2)]
        public bool GiveBackFiefs { get; }

        public NetworkClanLeaveKingdomApproved(string clanId, bool giveBackFiefs)
        {
            ClanId = clanId;
            GiveBackFiefs = giveBackFiefs;
        }
    }
}