using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Start;

/// <summary>
/// [Server -&gt; Clients] A player opened the auto-resolve simulation for a map event. Every other client whose own
/// party is in that map event opens the same simulation window as a passive spectator so it can watch the
/// server-streamed results. The initiating client (already showing it) and clients not in the event ignore it.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkOpenBattleSimulation : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventId;

    public NetworkOpenBattleSimulation(string mapEventId)
    {
        MapEventId = mapEventId;
    }
}
