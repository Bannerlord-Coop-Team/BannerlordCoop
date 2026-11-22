using Missions.Battles;

namespace E2E.Tests.Environment.Mock;

/// <summary>An <see cref="IShipStationSeats"/> driven by a test delegate; an unset delegate seats nobody.</summary>
public sealed class FakeShipStationSeats : IShipStationSeats
{
    /// <summary>Whether this client seated the agent with the given id.</summary>
    public Func<Guid, bool>? IsSeated { get; set; }

    /// <summary>Whether this client holds a remote owner's seat of the agent with the given id.</summary>
    public Func<Guid, bool>? IsApplied { get; set; }

    // Asks the test delegate.
    public bool IsOwnSeat(Guid agentId) => IsSeated?.Invoke(agentId) == true;

    // Asks the test delegate.
    public bool IsAppliedSeat(Guid agentId) => IsApplied?.Invoke(agentId) == true;
}
