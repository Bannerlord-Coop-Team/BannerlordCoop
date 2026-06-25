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
    /// Class for serializing <see cref="ProposeCallToWarAgreementDecision"/> class.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public class ProposeCallToWarAgreementDecisionData : KingdomDecisionData
    {
        private static readonly FieldInfo CalledKingdomField = typeof(ProposeCallToWarAgreementDecision).GetField(nameof(ProposeCallToWarAgreementDecision.CalledKingdom), BindingFlags.Instance | BindingFlags.Public);
        private static readonly FieldInfo KingdomToCallToWarAgainstField = typeof(ProposeCallToWarAgreementDecision).GetField(nameof(ProposeCallToWarAgreementDecision.KingdomToCallToWarAgainst), BindingFlags.Instance | BindingFlags.Public);
        private static readonly FieldInfo CallToWarCostField = typeof(ProposeCallToWarAgreementDecision).GetField(nameof(ProposeCallToWarAgreementDecision.CallToWarCost), BindingFlags.Instance | BindingFlags.Public);

        [ProtoMember(1)]
        public string CalledKingdomId { get; }
        [ProtoMember(2)]
        public string KingdomToCallToWarAgainstId { get; }
        [ProtoMember(3)]
        public int CallToWarCost { get; }

        public ProposeCallToWarAgreementDecisionData(string proposedClanId, string kingdomId, long triggerTime, bool isEnforced, bool notifyPlayer, bool playerExamined, string calledKingdomId, string kingdomToCallToWarAgainstId, int callToWarCost) : base(proposedClanId, kingdomId, triggerTime, isEnforced, notifyPlayer, playerExamined)
        {
            CalledKingdomId = calledKingdomId;
            KingdomToCallToWarAgainstId = kingdomToCallToWarAgainstId;
            CallToWarCost = callToWarCost;
        }

        /// <inheritdoc/>
        public override bool TryGetKingdomDecision(IObjectManager objectManager, out KingdomDecision kingdomDecision)
        {
            if (!TryGetProposerClanAndKingdom(objectManager, out Clan proposerClan, out Kingdom kingdom) ||
                !objectManager.TryGetObject(CalledKingdomId, out Kingdom calledKingdom) ||
                !objectManager.TryGetObject(KingdomToCallToWarAgainstId, out Kingdom kingdomToCallToWarAgainst))
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

            var proposeCallToWarAgreementDecision = ObjectHelper.SkipConstructor<ProposeCallToWarAgreementDecision>();
            SetKingdomDecisionProperties(proposeCallToWarAgreementDecision, proposerClan, kingdom);
            CalledKingdomField.SetValue(proposeCallToWarAgreementDecision, calledKingdom);
            KingdomToCallToWarAgainstField.SetValue(proposeCallToWarAgreementDecision, kingdomToCallToWarAgainst);
            CallToWarCostField.SetValue(proposeCallToWarAgreementDecision, CallToWarCost);
            proposeCallToWarAgreementDecision._allianceCampaignBehavior = allianceCampaignBehavior;
            kingdomDecision = proposeCallToWarAgreementDecision;
            return true;
        }
    }
}
