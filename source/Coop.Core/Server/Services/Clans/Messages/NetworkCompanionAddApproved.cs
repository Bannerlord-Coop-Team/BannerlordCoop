using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Clans.Messages
{
    /// <summary>
    /// Clan companion add is approved by server
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkCompanionAddApproved : ICommand
    {
        [ProtoMember(1)]
        public string ClanId { get; }
        [ProtoMember(2)]
        public string CompanionId { get; }

        public NetworkCompanionAddApproved(string clanId, string companionId)
        {
            ClanId = clanId;
            CompanionId = companionId;
        }
    }
}