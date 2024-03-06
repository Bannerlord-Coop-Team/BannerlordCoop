using Common.Messaging;
using ProtoBuf;
#nullable enable

namespace Coop.Core.Server.Services.Heroes.Messages
{
    /// <summary>
    /// Player escape is approved by server
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkPlayerEscapeApproved : ICommand
    {
        [ProtoMember(1)]
        public string HeroId { get; }

        public NetworkPlayerEscapeApproved(string heroId)
        {
            HeroId = heroId;
        }
    }
}