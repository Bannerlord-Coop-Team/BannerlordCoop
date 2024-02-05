using Common.Extensions;
using GameInterface.Services.ObjectManager;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;

namespace GameInterface.Services.Kingdoms.Data
{
    [ProtoContract(SkipConstructor = true)]
    public class KingdomPolicyDecisionData : KingdomDecisionData
    {
        private static Action<KingdomPolicyDecision, PolicyObject> SetPolicyMethod = typeof(KingdomPolicyDecision).GetField(nameof(KingdomPolicyDecision.Policy), BindingFlags.Instance | BindingFlags.Public).BuildUntypedSetter<KingdomPolicyDecision, PolicyObject>();
        private static Action<KingdomPolicyDecision, bool> SetIsInvertedDecisionMethod = typeof(KingdomPolicyDecision).GetField("_isInvertedDecision", BindingFlags.Instance | BindingFlags.NonPublic).BuildUntypedSetter<KingdomPolicyDecision, bool>();
        private static Action<KingdomPolicyDecision, List<PolicyObject>> SetKingdomPolicies = typeof(KingdomPolicyDecision).GetField("_kingdomPolicies", BindingFlags.Instance | BindingFlags.NonPublic).BuildUntypedSetter<KingdomPolicyDecision, List<PolicyObject>>();


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
            SetPolicyMethod(kingdomPolicyDecision, policyObject);
            SetIsInvertedDecisionMethod(kingdomPolicyDecision, IsInvertedDecision);
            SetKingdomPolicies(kingdomPolicyDecision, kingdomPolicies);
            kingdomDecision = kingdomPolicyDecision;
            return true;
        }
    }
}
