using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Messages
{
    internal class BattleStarted : IMessage
    {
        public MobileParty Attacker { get; }
        public MobileParty Defender { get; }

        public BattleStarted(MobileParty attacker, MobileParty defender)
        {
            Attacker = attacker;
            Defender = defender;
        }
    }
}