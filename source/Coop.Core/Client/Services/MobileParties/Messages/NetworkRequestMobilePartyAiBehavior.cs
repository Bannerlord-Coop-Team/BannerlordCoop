using Common.Messaging;
using GameInterface.Services.MobileParties.Data;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public record NetworkRequestMobilePartyAiBehavior : ICommand
    {
        [ProtoMember(1)]
        public AiBehaviorUpdateData BehaviorUpdateData { get; }

        public NetworkRequestMobilePartyAiBehavior(AiBehaviorUpdateData data)
        {
            BehaviorUpdateData = data;
        }
    }
}
