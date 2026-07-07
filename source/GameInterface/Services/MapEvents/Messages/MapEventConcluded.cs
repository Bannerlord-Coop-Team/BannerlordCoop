using Common.Messaging;

namespace GameInterface.Services.MapEvents.Messages;

/// <summary>
/// [Server, local] A map event reached a victory state (a side won). The server finalizes the event and closes
/// every involved player's encounter, so a concluded coop battle tears down without the player having to leave
/// the post-battle menu. Published by <c>MapEventHandler</c> when it applies a victory <c>BattleState</c>;
/// handled by <c>BattleHandler</c>.
/// </summary>
internal readonly struct MapEventConcluded : IEvent
{
    public readonly string MapEventId;
    public readonly string[] PlayerPartyIds;
    public readonly string SurrenderedPartyId;

    public MapEventConcluded(string mapEventId, string[] playerPartyIds = null, string surrenderedPartyId = null)
    {
        MapEventId = mapEventId;
        PlayerPartyIds = playerPartyIds ?? new string[0];
        SurrenderedPartyId = surrenderedPartyId;
    }
}
