using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.TroopRosters.Messages;

/// <summary>
/// Drops every depleted (zero-count) element from a roster, mirroring vanilla <c>RemoveZeroCounts</c>.
/// Roster-level, so it needs no per-character identity.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkTroopRosterRemoveZeroCounts : ICommand
{
    [ProtoMember(1)]
    public readonly string RosterId;

    public NetworkTroopRosterRemoveZeroCounts(string rosterId)
    {
        RosterId = rosterId;
    }
}
