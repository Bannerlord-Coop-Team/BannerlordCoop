using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.MapEvent.Messages
{
    /// <summary>
    /// End battle is approved by server
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkEndBattleApproved : ICommand
    {
        [ProtoMember(1)]
        public string partyId { get; }

        public NetworkEndBattleApproved(string partyId)
        {
            this.partyId = partyId;
        }
    }
}