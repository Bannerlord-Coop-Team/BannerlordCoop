using Common.Messaging;
using GameInterface.Services.MobileParties.Data;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages
{
    /// <summary>
    /// Requests change of party behavior on the campaign map.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkRequestMobilePartyBehavior : ICommand
    {
        [ProtoMember(1)]
        public PartyBehaviorUpdateData BehaviorUpdateData { get; }

        public NetworkRequestMobilePartyBehavior(PartyBehaviorUpdateData data)
        {
            BehaviorUpdateData = data;
        }
    }
}
