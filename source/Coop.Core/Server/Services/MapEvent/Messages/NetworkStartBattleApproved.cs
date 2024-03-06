using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.MapEvent.Messages
{
    /// <summary>
    /// Start battle is approved by server
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkStartBattleApproved : ICommand
    {
        [ProtoMember(1)]
        public string attackerPartyId { get; }
        [ProtoMember(2)]
        public string defenderPartyId { get; }

        public NetworkStartBattleApproved(string attackerPartyId, string defenderPartyId)
        {
            this.attackerPartyId = attackerPartyId;
            this.defenderPartyId = defenderPartyId;
        }
    }
}