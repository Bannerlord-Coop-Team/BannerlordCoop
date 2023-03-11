using Common.Messaging;
using GameInterface.Services.MobileParties.Data;

namespace GameInterface.Services.MobileParties.Messages
{
    public readonly struct UpdatePartyTargetPosition : ICommand
    {
        public TargetPositionData TargetPositionData { get; }

        public UpdatePartyTargetPosition(TargetPositionData targetPositionData)
        {
            TargetPositionData = targetPositionData;
        }
    }
}
