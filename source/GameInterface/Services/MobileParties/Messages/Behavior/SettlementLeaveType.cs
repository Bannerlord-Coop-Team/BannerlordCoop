namespace GameInterface.Services.MobileParties.Messages.Behavior;

/// <summary>
/// How a party leaves a settlement encounter.
/// </summary>
public enum SettlementLeaveType
{
    // Leaves through the gate without changing between land and sea.
    Leave,

    // Boards the fleet in the settlement's port.
    SetSail,

    // Goes ashore at a village gate and anchors the fleet in its port.
    Disembark,
}
