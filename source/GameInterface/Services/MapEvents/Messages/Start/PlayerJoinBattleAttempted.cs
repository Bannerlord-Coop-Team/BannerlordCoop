using Common.Messaging;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.Messages.Start;

/// <summary>
/// Local (client-side) event published when the local player party joins an existing battle. The join must be
/// performed authoritatively by the server, so <see cref="Handlers.BattleHandler"/> bridges this to a
/// <see cref="NetworkRequestJoinBattle"/>; the resulting add is replicated back through the normal map-event sync.
/// </summary>
internal readonly struct PlayerJoinBattleAttempted : IEvent
{
    public readonly PartyBase JoiningParty;
    public readonly MapEvent MapEvent;
    public readonly BattleSideEnum Side;

    public PlayerJoinBattleAttempted(PartyBase joiningParty, MapEvent mapEvent, BattleSideEnum side)
    {
        JoiningParty = joiningParty;
        MapEvent = mapEvent;
        Side = side;
    }
}
