using Common.Util;
using GameInterface.Services.ObjectManager;
using ProtoBuf;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Election;

namespace GameInterface.Services.Kingdoms.Data
{
    /// <summary>
    /// Class for serializing <see cref="StartAllianceDecision"/> class.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public class StartAllianceDecisionData : KingdomDecisionData
    {
        private static readonly FieldInfo KingdomToStartAllianceWithField = typeof(StartAllianceDecision).GetField(nameof(StartAllianceDecision.KingdomToStartAllianceWith), BindingFlags.Instance | BindingFlags.Public);

        [ProtoMember(1)]
        public string KingdomToStartAllianceWithId { get; }

        [ProtoMember(2)]
        public bool IsProposedByOpponent { get; }

        public StartAllianceDecisionData(string proposedClanId, string kingdomId, long triggerTime, bool isEnforced, bool notifyPlayer, bool playerExamined, string kingdomToStartAllianceWithId, bool isProposedByOpponent = false) : base(proposedClanId, kingdomId, triggerTime, isEnforced, notifyPlayer, playerExamined)
        {
            KingdomToStartAllianceWithId = kingdomToStartAllianceWithId;
            IsProposedByOpponent = isProposedByOpponent;
        }

        public bool TryGetProposerClanAndDecisionKingdom(IObjectManager objectManager, out Clan proposerClan, out Kingdom kingdom)
        {
            proposerClan = null;
            kingdom = null;
            if (!objectManager.TryGetObject(ProposerClanId, out proposerClan))
            {
                return false;
            }

            if (objectManager.TryGetObject(KingdomId, out kingdom))
            {
                return true;
            }

            if (!objectManager.TryGetObject(KingdomId, out Clan malformedKingdomIdClan) ||
                proposerClan.Kingdom == null ||
                malformedKingdomIdClan.Kingdom != proposerClan.Kingdom)
            {
                kingdom = null;
                return false;
            }

            kingdom = proposerClan.Kingdom;
            return true;
        }

        /// <inheritdoc/>
        public override bool TryGetKingdomDecision(IObjectManager objectManager, out KingdomDecision kingdomDecision)
        {
            if (!TryGetProposerClanAndDecisionKingdom(objectManager, out Clan proposerClan, out Kingdom kingdom) ||
                !objectManager.TryGetObject(KingdomToStartAllianceWithId, out Kingdom kingdomToStartAllianceWith))
            {
                kingdomDecision = null;
                return false;
            }

            IAllianceCampaignBehavior allianceCampaignBehavior = Campaign.Current?.GetCampaignBehavior<IAllianceCampaignBehavior>();
            if (allianceCampaignBehavior == null)
            {
                kingdomDecision = null;
                return false;
            }

            var startAllianceDecision = ObjectHelper.SkipConstructor<StartAllianceDecision>();
            SetKingdomDecisionProperties(startAllianceDecision, proposerClan, kingdom);
            KingdomToStartAllianceWithField.SetValue(startAllianceDecision, kingdomToStartAllianceWith);
            startAllianceDecision._allianceCampaignBehavior = allianceCampaignBehavior;
            kingdomDecision = startAllianceDecision;
            return true;
        }
    }
}
