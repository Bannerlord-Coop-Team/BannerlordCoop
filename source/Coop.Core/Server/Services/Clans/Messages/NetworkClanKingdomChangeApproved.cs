using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Clans.Messages
{
    /// <summary>
    /// Clan kingdom change is approved by server
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkClanKingdomChangeApproved : ICommand
    {
        [ProtoMember(1)]
        public string ClanId { get; }
        [ProtoMember(2)]
        public string NewKingdomId { get; }
        [ProtoMember(3)]
        public int DetailId { get; }
        [ProtoMember(4)]
        public int AwardMultiplier { get; }
        [ProtoMember(5)]
        public bool ByRebellion { get; }
        [ProtoMember(6)]
        public bool ShowNotification { get; }

        public NetworkClanKingdomChangeApproved(string clanId, string newKingdomId, int detailId, int awardMultiplier, bool byRebellion, bool showNotification)
        {
            ClanId = clanId;
            NewKingdomId = newKingdomId;
            DetailId = detailId;
            AwardMultiplier = awardMultiplier;
            ByRebellion = byRebellion;
            ShowNotification = showNotification;
        }
    }
}