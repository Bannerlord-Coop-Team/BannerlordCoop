using Common.Messaging;
using Coop.Core.Common.Services.MobileParties.Data;

namespace Coop.Core.Server.Services.MobileParties.Messages;

internal class NetworkPartyMovementRequested : IEvent
{
    public MobilePartyMovementData MovementData { get; }

    public NetworkPartyMovementRequested(MobilePartyMovementData movementData)
    {
        MovementData = movementData;
    }
}