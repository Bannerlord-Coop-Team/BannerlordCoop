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

    public NetworkMapEventInitialized(
        string mapEventId,
        bool isTerminal,
        string troopUpgradeTrackerId = null)
    {
        MapEventId = mapEventId;
        IsTerminal = isTerminal;
        TroopUpgradeTrackerId = troopUpgradeTrackerId;
    }
}
