using Common.Messaging;

namespace GameInterface.Services.Locations.Messages;

/// <summary>
/// Local (broker-only) event raised when the local player leaves the interior mission of a location — the
/// counterpart to <see cref="PlayerEnteredLocation"/>. A client handler turns it into a relay-membership
/// departure (<c>MissionLeft</c>) so the server drops the player from the instance's routing table.
/// </summary>
public record PlayerLeftLocation : IEvent
{
}
