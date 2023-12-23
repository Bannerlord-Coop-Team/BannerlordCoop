using Common.Messaging;
using Common.PacketHandlers;
using ProtoBuf;

namespace Coop.Core.Client.Services.Clans.Messages
{
    /// <summary>
    /// Request from client to server to add companion
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkAddCompanionRequest : ICommand
    {
        [ProtoMember(1)]
        public string ClanId { get; }
        [ProtoMember(2)]
        public string CompanionId { get; }

        public NetworkAddCompanionRequest(string clanId, string companionId)
        {
            ClanId = clanId;
            CompanionId = companionId;
        }
    }
}