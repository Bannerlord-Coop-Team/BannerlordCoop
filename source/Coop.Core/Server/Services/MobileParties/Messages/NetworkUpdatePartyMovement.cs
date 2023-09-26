using Common.Messaging;
using Coop.Core.Common.Services.MobileParties.Data;

namespace Coop.Core.Server.Services.MobileParties.Messages;

internal record NetworkUpdatePartyMovement : ICommand
{
    public MobilePartyMovementData MovementData { get; }

    public NetworkUpdatePartyMovement(MobilePartyMovementData movementData)
    {
        MovementData = movementData;
    }
}
