using Common.Messaging;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Messages;
using ProtoBuf;
using TaleWorlds.Library;

namespace Coop.Core.Common.Services.PartyMovement.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public record NetworkUpdatePartyTargetPosition : ICommand
    {
        [ProtoMember(1)]
        public string PartyId { get; }

        [ProtoMember(2)]
        public float TargetX { get; }

        [ProtoMember(3)]
        public float TargetY { get; }

        public PartyPositionData TargetPositionData
        {
            get => new PartyPositionData(PartyId, new Vec2(TargetX, TargetY));
        }

        public NetworkUpdatePartyTargetPosition(MessagePayload<ControlledPartyTargetPositionUpdated> obj) 
            : this(obj.What.TargetPositionData) { }

        public NetworkUpdatePartyTargetPosition(PartyPositionData positionData)
        {
            PartyId = positionData.PartyId;
            TargetX = positionData.TargetPosition.X;
            TargetY = positionData.TargetPosition.Y;
        }
    }
}