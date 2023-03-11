using ProtoBuf;

namespace GameInterface.Services.MobileParties.Data
{
    [ProtoContract]
    public readonly struct TargetPositionData
    {
        [ProtoMember(1)]
        public string ControlledHeroStringId { get; }
        [ProtoMember(2)]
        public float TargetPositionX { get; }
        [ProtoMember(3)]
        public float TargetPositionY { get; }

        public TargetPositionData(string controlledHeroStringId, float targetPositionX, float targetPositionY)
        {
            ControlledHeroStringId = controlledHeroStringId;
            TargetPositionX = targetPositionX;
            TargetPositionY = targetPositionY;
        }
    }
}
