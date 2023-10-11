using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Clans.Messages
{
    /// <summary>
    /// New heir is approved by server
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkNewHeirApproved : ICommand
    {
        [ProtoMember(1)]
        public string HeirHeroId { get; }
        [ProtoMember(2)]
        public string PlayerHeroId { get; }
        [ProtoMember(3)]
        public bool IsRetirement { get; }

        public NetworkNewHeirApproved(string heirHeroId, string playerHeroId, bool isRetirement)
        {
            HeirHeroId = heirHeroId;
            PlayerHeroId = playerHeroId;
            IsRetirement = isRetirement;
        }
    }
}