using Common.Messaging;
using GameInterface.Services.MobileParties.Data;
using ProtoBuf;

namespace Coop.Core.Server.Services.MobileParties.Messages
{
    /// <summary>
    /// Commands the update of a party's behavior on the campaign map.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkUpdatePartyBehavior : ICommand
    {
        [ProtoMember(1)]
        public PartyBehaviorUpdateData BehaviorUpdateData { get; }

        public NetworkUpdatePartyBehavior(PartyBehaviorUpdateData data)
        {
            BehaviorUpdateData = data;
        }
    }
}
