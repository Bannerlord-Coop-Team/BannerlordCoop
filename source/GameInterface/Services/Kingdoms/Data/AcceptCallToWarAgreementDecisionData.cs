using GameInterface.Services.ObjectManager;
using ProtoBuf;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Election;

namespace GameInterface.Services.Kingdoms.Data
{
    /// <summary>
    /// Class for serializing <see cref="AcceptCallToWarAgreementDecision"/> class.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public class AcceptCallToWarAgreementDecisionData : KingdomDecisionData
    {
        private static readonly FieldInfo CallToWarCostField = typeof(AcceptCallToWarAgreementDecision).GetField(nameof(AcceptCallToWarAgreementDecision.CallToWarCost), BindingFlags.Instance | BindingFlags.Public);

        [ProtoMember(1)]
        public string CallingKingdomId { get; }
        [ProtoMember(2)]
        public string KingdomToCallToWarAgainstId { get; }
        [ProtoMember(3)]
        public int CallToWarCost { get; }

        public AcceptCallToWarAgreementDecisionData(string proposedClanId, string kingdomId, long triggerTime, bool isEnforced, bool notifyPlayer, bool playerExamined, string callingKingdomId, string kingdomToCallToWarAgainstId, int callToWarCost) : base(proposedClanId, kingdomId, triggerTime, isEnforced, notifyPlayer, playerExamined)
        {
            CallingKingdomId = callingKingdomId;
            KingdomToCallToWarAgainstId = kingdomToCallToWarAgainstId;
            CallToWarCost = callToWarCost;
        }

        /// <inheritdoc/>
        public override bool TryGetKingdomDecision(IObjectManager objectManager, out KingdomDecision kingdomDecision)
        {
            if (!TryGetProposerClanAndKingdom(objectManager, out Clan proposerClan, out Kingdom kingdom) ||
                !objectManager.TryGetObject(CallingKingdomId, out Kingdom callingKingdom) ||
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

            AcceptCallToWarAgreementDecision acceptCallToWarAgreementDecision = new AcceptCallToWarAgreementDecision(proposerClan, callingKingdom, kingdomToCallToWarAgainst);
            SetKingdomDecisionProperties(acceptCallToWarAgreementDecision, proposerClan, kingdom);
            CallToWarCostField.SetValue(acceptCallToWarAgreementDecision, CallToWarCost);
            acceptCallToWarAgreementDecision._allianceCampaignBehavior = allianceCampaignBehavior;
            kingdomDecision = acceptCallToWarAgreementDecision;
            return true;
        }
    }
}
