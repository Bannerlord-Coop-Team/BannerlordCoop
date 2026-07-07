using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.SiegeEngines.Messages;

/// <summary>
/// Client asks the server to remove a deployed siege engine from its slot for one side of a siege.
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

    public NetworkRequestRemoveSiegeEngine(string siegeEventId, int side, int index, bool isRanged, bool moveToReserve)
    {
        SiegeEventId = siegeEventId;
        Side = side;
        Index = index;
        IsRanged = isRanged;
        MoveToReserve = moveToReserve;
    }
}
