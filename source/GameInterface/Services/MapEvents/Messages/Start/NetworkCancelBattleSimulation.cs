using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Start;

/// <summary>
/// Request cancellation of an unfinished battle simulation.
/// The server accepts this only from the client currently doing the simulation
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkCancelBattleSimulation : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventId;

    public NetworkCancelBattleSimulation(string mapEventId)
    {
        MapEventId = mapEventId;
    }
}