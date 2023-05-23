using Common.Messaging;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Messages;
using ProtoBuf;

namespace Coop.Core.Common.Services.PartyMovement.Messages
{
    [ProtoContract]
    public readonly struct NetworkUpdatePartyTargetPosition : ICommand
    {
        [ProtoMember(1)]
        public TargetPositionData TargetPositionData { get; }
        public NetworkUpdatePartyTargetPosition(MessagePayload<ControlledPartyTargetPositionUpdated> obj)
        {
            var payload = obj.What;

            TargetPositionData = payload.TargetPositionData;
        }

        public NetworkUpdatePartyTargetPosition(TargetPositionData positionData)
        {
            TargetPositionData = positionData;
        }
    }
}