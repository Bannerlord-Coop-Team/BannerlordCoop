using Common.Messaging;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Messages;
using ProtoBuf;

namespace Coop.Core.Common.Services.PartyMovement.Messages
{
    [ProtoContract]
    public record NetworkUpdatePartyTargetPosition : ICommand
    {
        [ProtoMember(1)]
        public PartyPositionData TargetPositionData { get; }
        public NetworkUpdatePartyTargetPosition(MessagePayload<ControlledPartyTargetPositionUpdated> obj)
        {
            var payload = obj.What;

            TargetPositionData = payload.TargetPositionData;
        }

        public NetworkUpdatePartyTargetPosition(PartyPositionData positionData)
        {
            TargetPositionData = positionData;
        }
    }
}