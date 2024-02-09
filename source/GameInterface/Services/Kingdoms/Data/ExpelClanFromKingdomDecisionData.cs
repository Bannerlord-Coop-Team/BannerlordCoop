using GameInterface.Services.ObjectManager;
using ProtoBuf;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;

namespace GameInterface.Services.Kingdoms.Data
{
    [ProtoContract(SkipConstructor = true)]
    public class ExpelClanFromKingdomDecisionData : KingdomDecisionData
    {
        private static readonly FieldInfo ClanToExpelField = typeof(ExpelClanFromKingdomDecision).GetField(nameof(ExpelClanFromKingdomDecision.ClanToExpel), BindingFlags.Instance | BindingFlags.Public);
        private static readonly FieldInfo OldKingdomField = typeof(ExpelClanFromKingdomDecision).GetField(nameof(ExpelClanFromKingdomDecision.OldKingdom), BindingFlags.Instance | BindingFlags.Public);

        [ProtoMember(1)]
        public string ClanToExpelId { get; }
        [ProtoMember(2)]
        public string OldKingdomId { get; }

        public ExpelClanFromKingdomDecisionData(string proposedClanId, string kingdomId, long triggerTime, bool isEnforced, bool notifyPlayer, bool playerExamined, string clanToExpelId, string oldKingdomId) : base(proposedClanId, kingdomId, triggerTime, isEnforced, notifyPlayer, playerExamined)
        {
            ClanToExpelId = clanToExpelId;
            OldKingdomId = oldKingdomId;
        }

        public override bool TryGetKingdomDecision(IObjectManager objectManager, out KingdomDecision kingdomDecision)
        {
            if (!TryGetProposerClanAndKingdom(objectManager, out Clan proposerClan, out Kingdom kingdom) ||
                !objectManager.TryGetObject(ClanToExpelId, out Clan clanToExpel) ||
                !objectManager.TryGetObject(OldKingdomId, out Kingdom oldKingdom))
            {
                kingdomDecision = null;
                return false;
            }

            ExpelClanFromKingdomDecision expelClanFromKingdomDecision = (ExpelClanFromKingdomDecision)FormatterServices.GetUninitializedObject(typeof(ExpelClanFromKingdomDecision));
            SetKingdomDecisionProperties(expelClanFromKingdomDecision, proposerClan, kingdom);
            ClanToExpelField.SetValue(expelClanFromKingdomDecision, clanToExpel);
            OldKingdomField.SetValue(expelClanFromKingdomDecision, oldKingdom);
            kingdomDecision = expelClanFromKingdomDecision;
            return true;
        }
    }
}
