using Common.Util;
using GameInterface.Services.ObjectManager;
using ProtoBuf;
using System.Linq;
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
            return TryGetKingdomReference(objectManager, KingdomId, out kingdom);
        }

        public static bool TryGetKingdomReference(IObjectManager objectManager, string kingdomId, out Kingdom kingdom)
        {
            kingdom = null;
            if (objectManager.TryGetObject(kingdomId, out object kingdomReference))
            {
                return TryGetKingdomFromReference(kingdomReference, out kingdom);
            }

            if (objectManager.TryGetObject(kingdomId, out Clan compactClan))
            {
                return TryGetKingdomFromReference(compactClan, out kingdom);
            }

            return objectManager.TryGetObject(kingdomId, out kingdom);
        }

        private static bool TryGetKingdomFromReference(object kingdomReference, out Kingdom kingdom)
        {
            kingdom = kingdomReference as Kingdom;
            if (kingdom != null) return true;

            var clan = kingdomReference as Clan;
            if (clan == null) return false;

            kingdom = clan.Kingdom;
            if (kingdom != null) return true;

            kingdom = Kingdom.All.FirstOrDefault(candidate =>
                candidate.RulingClan == clan || candidate.Clans.Contains(clan));
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
            return TryGetKingdomReference(objectManager, KingdomToStartAllianceWithId, out kingdom);
        }
    }
}
