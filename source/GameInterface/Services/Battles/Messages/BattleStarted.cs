using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Battles.Messages
{
    internal class BattleStarted : IMessage
    {
        public PartyBase Attacker { get; }
        public PartyBase Defender { get; }

        public BattleStarted(PartyBase attacker, PartyBase defender)
        {
            Attacker = attacker;
            Defender = defender;
        }
    }
}