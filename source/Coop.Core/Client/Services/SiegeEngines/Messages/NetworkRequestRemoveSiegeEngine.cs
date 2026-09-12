using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.SiegeEngines.Messages;

/// <summary>
/// Client asks the server to remove a deployed siege engine from its slot for one side of a siege, conditional
/// on the occupant and server-issued slot generation the UI observed.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkRequestRemoveSiegeEngine : ICommand
{
    [ProtoMember(1)]
    public string SiegeEventId { get; }
    [ProtoMember(2)]
    public int Side { get; }
    [ProtoMember(3)]
    public int Index { get; }
    [ProtoMember(4)]
    public bool IsRanged { get; }
    [ProtoMember(5)]
    public bool MoveToReserve { get; }
    [ProtoMember(6)]
    public string ExpectedOccupantId { get; }
    [ProtoMember(7)]
    public long ExpectedRevision { get; }
    [ProtoMember(8)]
    public string RevisionEpoch { get; }

    public NetworkRequestRemoveSiegeEngine(
        string siegeEventId,
        int side,
        int index,
        bool isRanged,
        bool moveToReserve,
        string expectedOccupantId,
        long expectedRevision,
        string revisionEpoch)
    {
        SiegeEventId = siegeEventId;
        Side = side;
        Index = index;
        IsRanged = isRanged;
        MoveToReserve = moveToReserve;
        ExpectedOccupantId = expectedOccupantId;
        ExpectedRevision = expectedRevision;
        RevisionEpoch = revisionEpoch;
    }
}
