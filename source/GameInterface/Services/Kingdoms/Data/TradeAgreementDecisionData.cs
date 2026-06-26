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
    /// Class for serializing <see cref="TradeAgreementDecision"/> class.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public class TradeAgreementDecisionData : KingdomDecisionData
    {
        private static readonly FieldInfo TargetKingdomField = typeof(TradeAgreementDecision).GetField(nameof(TradeAgreementDecision.TargetKingdom), BindingFlags.Instance | BindingFlags.Public);

        [ProtoMember(1)]
        public string TargetKingdomId { get; }

        public TradeAgreementDecisionData(string proposedClanId, string kingdomId, long triggerTime, bool isEnforced, bool notifyPlayer, bool playerExamined, string targetKingdomId) : base(proposedClanId, kingdomId, triggerTime, isEnforced, notifyPlayer, playerExamined)
        {
            TargetKingdomId = targetKingdomId;
        }

        /// <inheritdoc/>
        public override bool TryGetKingdomDecision(IObjectManager objectManager, out KingdomDecision kingdomDecision)
        {
            if (!TryGetProposerClanAndKingdom(objectManager, out Clan proposerClan, out Kingdom kingdom) ||
                !objectManager.TryGetObject(TargetKingdomId, out Kingdom targetKingdom))
            {
                kingdomDecision = null;
                return false;
            }

            ITradeAgreementsCampaignBehavior tradeAgreementsCampaignBehavior = Campaign.Current?.GetCampaignBehavior<ITradeAgreementsCampaignBehavior>();
            if (tradeAgreementsCampaignBehavior == null)
            {
                kingdomDecision = null;
                return false;
            }

            var tradeAgreementDecision = ObjectHelper.SkipConstructor<TradeAgreementDecision>();
            SetKingdomDecisionProperties(tradeAgreementDecision, proposerClan, kingdom);
            TargetKingdomField.SetValue(tradeAgreementDecision, targetKingdom);
            tradeAgreementDecision._tradeAgreementsCampaignBehavior = tradeAgreementsCampaignBehavior;
            kingdomDecision = tradeAgreementDecision;
            return true;
        }
    }
}
