using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Battles.Messages;

internal readonly struct BattleStarted : IMessage
{
    public readonly PartyBase Attacker;
    public readonly PartyBase Defender;

    public BattleStarted(PartyBase attacker, PartyBase defender)
    {
        Attacker = attacker;
        Defender = defender;
    }
}