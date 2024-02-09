using GameInterface.Services.ObjectManager;
using ProtoBuf;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;

namespace GameInterface.Services.Kingdoms.Data
{
    /// <summary>
    /// Class for serializing <see cref="KingdomPolicyDecision"> class.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public class KingdomPolicyDecisionData : KingdomDecisionData
    {
        private static readonly FieldInfo PolicyField = typeof(KingdomPolicyDecision).GetField(nameof(KingdomPolicyDecision.Policy), BindingFlags.Instance | BindingFlags.Public);
        private static readonly FieldInfo IsInvertedDecisionField = typeof(KingdomPolicyDecision).GetField("_isInvertedDecision", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo KingdomPoliciesField = typeof(KingdomPolicyDecision).GetField("_kingdomPolicies", BindingFlags.Instance | BindingFlags.NonPublic);


        [ProtoMember(1)]
        public string PolicyObjectId { get; }

        [ProtoMember(2)]
        public bool IsInvertedDecision { get; }
        [ProtoMember(3)]
        public List<string> KingdomPolicies { get; }

        public KingdomPolicyDecisionData(string proposedClanId, string kingdomId, long triggerTime, bool isEnforced, bool notifyPlayer, bool playerExamined, string policyObjectId, bool isInvertedDecision, List<string> kingdomPolicies) : base(proposedClanId, kingdomId, triggerTime, isEnforced, notifyPlayer, playerExamined)
        {
            PolicyObjectId = policyObjectId;
            IsInvertedDecision = isInvertedDecision;
            KingdomPolicies = kingdomPolicies;
        }

        /// <inheritdoc/>
        public override bool TryGetKingdomDecision(IObjectManager objectManager, out KingdomDecision kingdomDecision)
        {
            if (!TryGetProposerClanAndKingdom(objectManager, out Clan proposerClan, out Kingdom kingdom) ||
                !objectManager.TryGetObject(PolicyObjectId, out PolicyObject policyObject))
            {
                kingdomDecision = null;
                return false;
            }

            List<PolicyObject> kingdomPolicies = new List<PolicyObject>();
            foreach (string policyObjectId in KingdomPolicies)
            {
                if (!objectManager.TryGetObject(policyObjectId, out PolicyObject kingdomPolicy))
                {
                    kingdomDecision = null;
                    return false;
                }
                kingdomPolicies.Add(kingdomPolicy);
            }

            KingdomPolicyDecision kingdomPolicyDecision = (KingdomPolicyDecision)FormatterServices.GetUninitializedObject(typeof(KingdomPolicyDecision));
            SetKingdomDecisionProperties(kingdomPolicyDecision, proposerClan, kingdom);
            PolicyField.SetValue(kingdomPolicyDecision, policyObject);
            IsInvertedDecisionField.SetValue(kingdomPolicyDecision, IsInvertedDecision);
            KingdomPoliciesField.SetValue(kingdomPolicyDecision, kingdomPolicies);
            kingdomDecision = kingdomPolicyDecision;
            return true;
        }
    }
}
