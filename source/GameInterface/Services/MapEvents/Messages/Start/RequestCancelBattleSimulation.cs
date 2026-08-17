using Common.Messaging;

namespace GameInterface.Services.MapEvents.Messages.Start;

/// <summary>
/// Raised when the client closes an unfinished battle simulation.
/// The simulation handler forwards it to the server.
/// </summary>
internal readonly struct RequestCancelBattleSimulation : IEvent
{
    public readonly string MapEventId;

    public RequestCancelBattleSimulation(string mapEventId)
    {
        MapEventId = mapEventId;
    }
}