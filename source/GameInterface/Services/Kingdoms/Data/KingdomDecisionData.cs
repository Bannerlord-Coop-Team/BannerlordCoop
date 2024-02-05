using Common.Extensions;
using GameInterface.Services.ObjectManager;
using ProtoBuf;
using System;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;

namespace GameInterface.Services.Kingdoms.Data
{
    [ProtoContract(SkipConstructor = true)]
    [ProtoInclude(6, nameof(DeclareWarDecisionData))]
    [ProtoInclude(7, nameof(ExpelClanFromKingdomDecisionData))]
    [ProtoInclude(8, nameof(KingdomPolicyDecisionData))]
    [ProtoInclude(9, nameof(KingSelectionKingdomDecisionData))]
    [ProtoInclude(10, nameof(MakePeaceKingdomDecisionData))]
    [ProtoInclude(11, nameof(SettlementClaimantDecisionData))]
    [ProtoInclude(12, nameof(SettlementClaimantPreliminaryDecisionData))]
    public abstract class KingdomDecisionData
    {
        private static Action<KingdomDecision, Kingdom> SetKingdomMethod = typeof(KingdomDecision).GetField("_kingdom", BindingFlags.Instance | BindingFlags.NonPublic).BuildUntypedSetter<KingdomDecision, Kingdom>();
        private static Action<KingdomDecision, Clan> SetProposerClanMethod = typeof(KingdomDecision).GetProperty(nameof(KingdomDecision.ProposerClan), BindingFlags.Instance | BindingFlags.Public).BuildUntypedSetter<KingdomDecision, Clan>();
        private static Action<KingdomDecision, CampaignTime> SetTriggerTimeMethod = typeof(KingdomDecision).GetProperty(nameof(KingdomDecision.TriggerTime), BindingFlags.Instance | BindingFlags.Public).BuildUntypedSetter<KingdomDecision, CampaignTime>();
        private static ConstructorInfo CampaignTimeCtr = typeof(CampaignTime).GetConstructor(BindingFlags.NonPublic, null, new Type[] { typeof(long) }, null);

        [ProtoMember(1)]
        public string ProposerClanId { get; }
        [ProtoMember(2)]
        public string KingdomId { get; }
        [ProtoMember(3)]
        public long TriggerTime { get; }
        [ProtoMember(4)]
        public bool IsEnforced { get; }
        [ProtoMember(5)]
        public bool NotifyPlayer { get; }
        [ProtoMember(6)]
        public bool PlayerExamined { get; }

        public KingdomDecisionData(string proposedClanId, string kingdomId, long triggerTime, bool isEnforced, bool notifyPlayer, bool playerExamined)
        {
            ProposerClanId = proposedClanId;
            KingdomId = kingdomId;
            TriggerTime = triggerTime;
            IsEnforced = isEnforced;
            NotifyPlayer = notifyPlayer;
            PlayerExamined = playerExamined;
        }

        protected bool TryGetProposerClanAndKingdom(IObjectManager objectManager, out Clan proposerClan, out Kingdom kingdom)
        {
            if (!objectManager.TryGetObject(ProposerClanId, out proposerClan) |
                !objectManager.TryGetObject(KingdomId, out kingdom))
            {
                return false;
            }
            return true;
        }

        protected void SetKingdomDecisionProperties(KingdomDecision kingdomDecision, Clan proposerClan, Kingdom kingdom)
        {
            SetProposerClanMethod(kingdomDecision, proposerClan);
            SetKingdomMethod(kingdomDecision, kingdom);
            kingdomDecision.IsEnforced = IsEnforced;
            kingdomDecision.NotifyPlayer = NotifyPlayer;
            kingdomDecision.PlayerExamined = PlayerExamined;
            SetTriggerTimeMethod(kingdomDecision, (CampaignTime)CampaignTimeCtr.Invoke(new object[] { TriggerTime }));
        }


        public abstract bool TryGetKingdomDecision(IObjectManager objectManager,out KingdomDecision kingdomDecision);
    }
}
