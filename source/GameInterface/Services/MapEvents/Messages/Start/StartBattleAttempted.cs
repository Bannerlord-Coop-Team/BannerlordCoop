using Common.Messaging;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MapEvents.Messages.Start;

internal readonly struct StartBattleAttempted : IEvent
{
    public readonly PartyBase Attacker;
    public readonly PartyBase Defender;
    public readonly Settlement Settlement;
    public readonly MapEvent.BattleTypes BattleType;
    public StartBattleAttempted(PartyBase attacker, PartyBase defender, Settlement settlement, MapEvent.BattleTypes battleType)
    {
        Attacker = attacker;
        Defender = defender;
        Settlement = settlement;
        BattleType = battleType;
    }
}
