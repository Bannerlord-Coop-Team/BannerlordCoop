using GameInterface.Services.ObjectManager;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;

namespace GameInterface.Services.Kingdoms.Data
{
    [ProtoContract(SkipConstructor = true)]
    public class ExpelClanFromKingdomDecisionData : KingdomDecisionData
    {
        [ProtoMember(1)]
        public string ClanToExpelId { get; }

        public ExpelClanFromKingdomDecisionData(string proposedClanId, int triggerTime, bool isEnforced, bool notifyPlayer, bool playerExamined, string clanToExpelId) : base(proposedClanId, triggerTime, isEnforced, notifyPlayer, playerExamined)
        {
            ClanToExpelId = clanToExpelId;
        }

        public override bool TryGetKingdomDecision(IObjectManager objectManager, out KingdomDecision kingdomDecision)
        {
            if (!objectManager.TryGetObject(ProposerClanId, out Clan proposerClan))
            {
                kingdomDecision = null;
                return false;
            }

            if (!objectManager.TryGetObject(ClanToExpelId, out Clan clanToExpel))
            {
                kingdomDecision = null;
                return false;
            }

            kingdomDecision = new ExpelClanFromKingdomDecision(proposerClan, clanToExpel);
            SetKingdomDecisionProperties(kingdomDecision);
            return true;
        }
    }
}
