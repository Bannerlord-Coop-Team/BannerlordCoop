using Common.Messaging;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Messages.Start;

internal readonly struct MapEventInitialize : IEvent
{
    public readonly MapEvent MapEvent;
    public readonly MapEvent.BattleTypes BattleType;
    public readonly PartyBase AttackerParty;
    public readonly PartyBase DefenderParty;

    public MapEventInitialize(MapEvent mapEvent, MapEvent.BattleTypes battleType, PartyBase attackerParty, PartyBase defenderParty)
    {
        MapEvent = mapEvent;
        BattleType = battleType;
        AttackerParty = attackerParty;
        DefenderParty = defenderParty;
    }
}
