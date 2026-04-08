using Common.Messaging;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Messages;

internal record MapEventInitialize : IEvent
{
    public MapEvent MapEvent { get; }
    public MapEvent.BattleTypes BattleType { get; }
    public PartyBase AttackerParty { get;  }
    public PartyBase DefenderParty { get; }

    public MapEventInitialize(MapEvent mapEvent, MapEvent.BattleTypes battleType, PartyBase attackerParty, PartyBase defenderParty)
    {
        MapEvent = mapEvent;
        BattleType = battleType;
        AttackerParty = attackerParty;
        DefenderParty = defenderParty;
    }
}
