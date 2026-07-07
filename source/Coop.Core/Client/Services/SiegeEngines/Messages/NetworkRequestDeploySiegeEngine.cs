using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.SiegeEngines.Messages;

/// <summary>
/// Client asks the server to build/deploy a siege engine at a slot for one side of a siege.
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

    public NetworkRequestDeploySiegeEngine(string siegeEventId, int side, string engineTypeId, int index)
    {
        SiegeEventId = siegeEventId;
        Side = side;
        EngineTypeId = engineTypeId;
        Index = index;
    }
}
