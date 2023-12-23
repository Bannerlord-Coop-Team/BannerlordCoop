using Common.Messaging;
using Common.PacketHandlers;
using ProtoBuf;

namespace Coop.Core.Client.Services.Clans.Messages
{
    /// <summary>
    /// Request from client to server to adopt hero
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkAdoptHeroRequest : ICommand
    {
        [ProtoMember(1)]
        public string HeroId { get; }
        [ProtoMember(2)]
        public string PlayerClanId { get; }
        [ProtoMember(3)]
        public string PlayerHeroId { get; }

        public NetworkAdoptHeroRequest(string heroId, string playerClanId, string playerHeroId)
        {
            HeroId = heroId;
            PlayerClanId = playerClanId;
            PlayerHeroId = playerHeroId;
        }
    }
}