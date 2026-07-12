using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Start;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkMapEventInitialized : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventId;

    [ProtoMember(2)]
    public readonly bool IsTerminal;

    [ProtoMember(3)]
    public readonly string TroopUpgradeTrackerId;

    [ProtoMember(4)]
    public readonly string ComponentId;

    [ProtoMember(5)]
    public readonly string VisualId;

    public NetworkMapEventInitialized(
        string mapEventId,
        bool isTerminal,
        string troopUpgradeTrackerId = null,
        string componentId = null,
        string visualId = null)
    {
        MapEventId = mapEventId;
        IsTerminal = isTerminal;
        TroopUpgradeTrackerId = troopUpgradeTrackerId;
        ComponentId = componentId;
        VisualId = visualId;
    }
}
