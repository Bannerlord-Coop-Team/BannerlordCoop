using Common.Messaging;
using GameInterface.Services.MobileParties.Data;
using ProtoBuf;

namespace Coop.Core.Server.Services.MobileParties.Messages
{
    /// <summary>
    /// Commands the update of a party's behavior on the campaign map.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkUpdatePartyAiBehavior : ICommand
    {
        [ProtoMember(1)]
        public AiBehaviorUpdateData BehaviorUpdateData { get; }

        public NetworkUpdatePartyAiBehavior(AiBehaviorUpdateData data)
        {
            BehaviorUpdateData = data;
        }
    }
}
