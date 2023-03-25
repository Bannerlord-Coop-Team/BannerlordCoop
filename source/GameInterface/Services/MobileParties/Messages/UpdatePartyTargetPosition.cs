using Common.Messaging;
using GameInterface.Services.MobileParties.Data;
using System;

namespace GameInterface.Services.MobileParties.Messages
{
    public readonly struct UpdatePartyTargetPosition : ICommand
    {
        public TargetPositionData TargetPositionData { get; }

        public Guid TransactionID => throw new NotImplementedException();

        public UpdatePartyTargetPosition(TargetPositionData targetPositionData)
        {
            TargetPositionData = targetPositionData;
        }
    }
}
