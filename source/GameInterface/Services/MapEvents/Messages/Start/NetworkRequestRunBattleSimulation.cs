using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Start;

/// <summary>
/// [Client -&gt; Server] Asks the server to run the authoritative auto-resolve simulation for the
/// given map event. Casualties and the resulting <c>BattleState</c> are replicated back through the
/// existing TroopRoster / MapEvent sync; completion is signalled by <see cref="NetworkBattleSimulationFinished"/>.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRequestRunBattleSimulation : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventId;

    public NetworkRequestRunBattleSimulation(string mapEventId)
    {
        MapEventId = mapEventId;
    }
}
