using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Clans.Messages
{
    /// <summary>
    /// Clan influence change is approved by server
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    [DontLogMessage]
    public record NetworkClanChangeInfluenceApproved : ICommand
    {
        [ProtoMember(1)]
        public string ClanId { get; }
        [ProtoMember(2)]
        public float Amount { get; }

        public NetworkClanChangeInfluenceApproved(string clanId, float amount)
        {
            ClanId = clanId;
            Amount = amount;
        }
    }
}