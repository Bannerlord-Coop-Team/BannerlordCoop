using Common.Messaging;

namespace GameInterface.Services.MapEvents.Messages.Start;

/// <summary>
/// [Client, local] Raised by the playback clock when the simulation should advance. The handler
/// forwards it to the server as a <see cref="NetworkAdvanceBattleSimulation"/>.
/// </summary>
internal readonly struct RequestAdvanceBattleSimulation : IEvent
{
    public readonly string MapEventId;
    public readonly int Rounds;

    public RequestAdvanceBattleSimulation(string mapEventId, int rounds)
    {
        MapEventId = mapEventId;
        Rounds = rounds;
    }
}
