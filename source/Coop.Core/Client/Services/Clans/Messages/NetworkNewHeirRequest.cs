using Common.Messaging;
using Common.PacketHandlers;
using ProtoBuf;

namespace Coop.Core.Client.Services.Clans.Messages
{
    /// <summary>
    /// Request from client to server for new heir
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkNewHeirRequest : ICommand
    {
        [ProtoMember(1)]
        public string HeirHeroId { get; }
        [ProtoMember(2)]
        public string PlayerHeroId { get; }
        [ProtoMember(3)]
        public bool IsRetirement { get; }

        public NetworkNewHeirRequest(string heirHeroId, string playerHeroId, bool isRetirement)
        {
            HeirHeroId = heirHeroId;
            PlayerHeroId = playerHeroId;
            IsRetirement = isRetirement;
        }
    }
}