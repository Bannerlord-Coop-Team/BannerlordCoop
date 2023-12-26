using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Clans.Messages
{
    /// <summary>
    /// Clan renown add is approved by server
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkRenownAddApproved : ICommand
    {
        [ProtoMember(1)]
        public string ClanId { get; }
        [ProtoMember(2)]
        public float Amount { get; }
        [ProtoMember(3)]
        public bool ShouldNotify { get; }

        public NetworkRenownAddApproved(string clanId, float amount, bool shouldNotify)
        {
            ClanId = clanId;
            Amount = amount;
            ShouldNotify = shouldNotify;
        }
    }
}