using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages;

/// <summary>
/// Server -&gt; all clients: a player-vs-player map event has been finalized. Every client that controls one of
/// <see cref="PartyIds"/> (the player parties that were on either side) closes its now-defunct encounter menu. This
/// is the reliable, server-addressed close — it replaces each client racing its own local map-event teardown.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkClosePvpEncounter : ICommand
{
    [ProtoMember(1)]
    public readonly string[] PartyIds;

    public NetworkClosePvpEncounter(string[] partyIds)
    {
        PartyIds = partyIds;
    }
}
