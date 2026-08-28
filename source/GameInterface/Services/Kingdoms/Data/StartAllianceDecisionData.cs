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

            if (TryGetDecisionKingdomReference(objectManager, out kingdom))
            {
                return true;
            }

            kingdom = proposerClan.Kingdom;
            return kingdom != null;
        }

        private bool TryGetDecisionKingdomReference(IObjectManager objectManager, out Kingdom kingdom)
        {
            kingdom = null;
            if (objectManager.TryGetObject(KingdomId, out object kingdomReference))
            {
                return TryGetKingdomFromReference(kingdomReference, out kingdom);
            }

            if (objectManager.TryGetObject(KingdomId, out Clan compactClan))
            {
                kingdom = compactClan.Kingdom;
                return kingdom != null;
            }

            return objectManager.TryGetObject(KingdomId, out kingdom);
        }

        private static bool TryGetKingdomFromReference(object kingdomReference, out Kingdom kingdom)
        {
            kingdom = kingdomReference as Kingdom;
            if (kingdom != null) return true;

            kingdom = (kingdomReference as Clan)?.Kingdom;
            return kingdom != null;
        }

        /// <inheritdoc/>
        public override bool TryGetKingdomDecision(IObjectManager objectManager, out KingdomDecision kingdomDecision)
        {
            if (!TryGetProposerClanAndDecisionKingdom(objectManager, out Clan proposerClan, out Kingdom kingdom) ||
                !TryGetTargetKingdomReference(objectManager, out Kingdom kingdomToStartAllianceWith))
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

        private bool TryGetTargetKingdomReference(IObjectManager objectManager, out Kingdom kingdom)
        {
            kingdom = null;
            if (objectManager.TryGetObject(KingdomToStartAllianceWithId, out object kingdomReference))
            {
                if (kingdomReference is Kingdom serializedKingdom)
                {
                    kingdom = serializedKingdom;
                    return true;
                }

                if (kingdomReference is Clan serializedClan && serializedClan.Kingdom != null)
                {
                    kingdom = serializedClan.Kingdom;
                    return true;
                }

                return false;
            }

            if (objectManager.TryGetObject(KingdomToStartAllianceWithId, out Clan compactClan) && compactClan.Kingdom != null)
            {
                kingdom = compactClan.Kingdom;
                return true;
            }

            return objectManager.TryGetObject(KingdomToStartAllianceWithId, out kingdom);
        }
    }
}
