using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Start;

/// <summary>
/// [Server -&gt; Clients] A player launched the field-battle mission for a map event (its sides are now
/// mission-ready). Every client whose own party is in that event records the mission as in progress so its
/// encounter menu greys out the auto-resolve (simulation) options — a map event is fought as a live mission XOR
/// an auto-resolve, never both. The mission counterpart to <see cref="NetworkOpenBattleSimulation"/>.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkBattleMissionStarted : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventId;

    public NetworkBattleMissionStarted(string mapEventId)
    {
        MapEventId = mapEventId;
    }
}
