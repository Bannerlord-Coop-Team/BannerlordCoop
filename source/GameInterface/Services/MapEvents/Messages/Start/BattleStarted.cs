using Common.Messaging;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Messages.Start;

internal readonly struct BattleStarted : IEvent
{
    public readonly MapEvent MapEvent;
    public readonly PartyBase AttackerParty;
    public readonly PartyBase DefenderParty;

    public BattleStarted(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
    {
        MapEvent = mapEvent;
        AttackerParty = attackerParty;
        DefenderParty = defenderParty;
    }
}