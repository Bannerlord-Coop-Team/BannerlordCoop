using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Start;

/// <summary>
/// [Server -> Client] Sent to the requesting client once the server has finished running the
/// authoritative simulation for the map event. The client uses it to close its open simulation
/// screen, by which point all casualty / <c>BattleState</c> sync messages have already been applied.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkBattleSimulationFinished : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventId;

    public NetworkBattleSimulationFinished(string mapEventId)
    {
        MapEventId = mapEventId;
    }
}
