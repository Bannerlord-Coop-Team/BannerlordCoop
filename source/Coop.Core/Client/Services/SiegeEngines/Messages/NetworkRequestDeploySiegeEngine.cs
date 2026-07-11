using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.SiegeEngines.Messages;

/// <summary>
/// Client asks the server to build/deploy a siege engine at a slot for one side of a siege, conditional on
/// the occupant and server-issued slot generation the UI observed.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkRequestDeploySiegeEngine : ICommand
{
    [ProtoMember(1)]
    public string SiegeEventId { get; }
    [ProtoMember(2)]
    public int Side { get; }
    [ProtoMember(3)]
    public string EngineTypeId { get; }
    [ProtoMember(4)]
    public int Index { get; }
    [ProtoMember(5)]
    public string ExpectedOccupantId { get; }
    [ProtoMember(6)]
    public long ExpectedRevision { get; }
    [ProtoMember(7)]
    public string RevisionEpoch { get; }

    public NetworkRequestDeploySiegeEngine(
        string siegeEventId,
        int side,
        string engineTypeId,
        int index,
        string expectedOccupantId,
        long expectedRevision,
        string revisionEpoch)
    {
        SiegeEventId = siegeEventId;
        Side = side;
        EngineTypeId = engineTypeId;
        Index = index;
        ExpectedOccupantId = expectedOccupantId;
        ExpectedRevision = expectedRevision;
        RevisionEpoch = revisionEpoch;
    }
}
