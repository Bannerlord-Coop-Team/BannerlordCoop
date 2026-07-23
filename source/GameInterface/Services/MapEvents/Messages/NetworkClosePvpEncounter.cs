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

    [ProtoMember(2)]
    public readonly string SurrenderedPartyId;

    [ProtoMember(3)]
    public readonly string MapEventId;

    public NetworkClosePvpEncounter(string[] partyIds, string surrenderedPartyId = null, string mapEventId = null)
    {
        PartyIds = partyIds;
        SurrenderedPartyId = surrenderedPartyId;
        MapEventId = mapEventId;
    }
}
