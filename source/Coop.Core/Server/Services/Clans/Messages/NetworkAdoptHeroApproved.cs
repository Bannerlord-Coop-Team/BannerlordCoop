using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Clans.Messages
{
    /// <summary>
    /// Hero adoption is approved by server
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkAdoptHeroApproved : ICommand
    {
        [ProtoMember(1)]
        public string HeroId { get; }
        [ProtoMember(2)]
        public string PlayerClanId { get; }
        [ProtoMember(3)]
        public string PlayerHeroId { get; }

        public NetworkAdoptHeroApproved(string heroId, string playerClanId, string playerHeroId)
        {
            HeroId = heroId;
            PlayerClanId = playerClanId;
            PlayerHeroId = playerHeroId;
        }
    }
}