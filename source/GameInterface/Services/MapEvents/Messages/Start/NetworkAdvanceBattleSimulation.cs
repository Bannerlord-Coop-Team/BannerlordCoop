using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Start;

/// <summary>
/// [Client -> Server] Asks the server to resolve up to <see cref="MaxRounds"/> more rounds of an
/// in-progress battle simulation. The client sends these paced by its own playback clock (one per
/// in-game second, faster while fast-forwarding, all-at-once on skip), so the authoritative map event
/// advances in lockstep with what the player sees rather than resolving instantly.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkAdvanceBattleSimulation : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventId;
    [ProtoMember(2)]
    public readonly int MaxRounds;

    public NetworkAdvanceBattleSimulation(string mapEventId, int maxRounds)
    {
        MapEventId = mapEventId;
        MaxRounds = maxRounds;
    }
}
