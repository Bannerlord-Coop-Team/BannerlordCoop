using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.Heroes.Messages
{
    /// <summary>
    /// Request from client to server for player escape
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkPlayerEscapeRequest : ICommand
    {
        [ProtoMember(1)]
        public string HeroId { get; }

        public NetworkPlayerEscapeRequest(string heroId)
        {
            HeroId = heroId;
        }
    }
}