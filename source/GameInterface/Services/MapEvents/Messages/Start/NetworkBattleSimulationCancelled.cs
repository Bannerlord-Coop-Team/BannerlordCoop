using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Start;

/// <summary>
/// Confirms that an unfinished battle simulation was canceled.
/// Clients discard the matching replay without invoking normal battle result handling.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkBattleSimulationCancelled : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventId;

    public NetworkBattleSimulationCancelled(string mapEventId)
    {
        MapEventId = mapEventId;
    }
}