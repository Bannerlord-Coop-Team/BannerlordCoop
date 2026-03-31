using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Battles.Messages
{
    internal class BattleStarted : IMessage
    {
        public MobileParty Attacker { get; }
        public PartyBase Defender { get; }

        public BattleStarted(MobileParty attacker, PartyBase defender)
        {
            Attacker = attacker;
            Defender = defender;
        }
    }
}