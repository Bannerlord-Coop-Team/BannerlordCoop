using ProtoBuf;
using System;

namespace GameInterface.Services.MobileParties.Data
{
    [ProtoContract]
    public readonly struct TargetPositionData
    {
        [ProtoMember(1)]
        public Guid ControlledHeroId { get; }
        [ProtoMember(2)]
        public float TargetPositionX { get; }
        [ProtoMember(3)]
        public float TargetPositionY { get; }

        public TargetPositionData(Guid controlledHeroId, float targetPositionX, float targetPositionY)
        {
            ControlledHeroId = controlledHeroId;
            TargetPositionX = targetPositionX;
            TargetPositionY = targetPositionY;
        }
    }
}
