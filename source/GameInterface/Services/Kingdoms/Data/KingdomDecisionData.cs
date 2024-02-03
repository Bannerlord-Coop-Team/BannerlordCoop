using GameInterface.Services.ObjectManager;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Election;

namespace GameInterface.Services.Kingdoms.Data
{
    [ProtoContract(SkipConstructor = true)]
    [ProtoInclude(6, nameof(DeclareWarDecisionData))]
    [ProtoInclude(7, nameof(ExpelClanFromKingdomDecisionData))]
    [ProtoInclude(8, nameof(KingdomPolicyDecisionData))]
    public abstract class KingdomDecisionData
    {
        [ProtoMember(1)]
        public string ProposerClanId { get; }
        [ProtoMember(2)]
        public int TriggerTime { get; }
        [ProtoMember(3)]
        public bool IsEnforced { get; }
        [ProtoMember(4)]
        public bool NotifyPlayer { get; }
        [ProtoMember(5)]
        public bool PlayerExamined { get; }

        public KingdomDecisionData(string proposedClanId, int triggerTime, bool isEnforced, bool notifyPlayer, bool playerExamined)
        {
            ProposerClanId = proposedClanId;
            TriggerTime = triggerTime;
            IsEnforced = isEnforced;
            NotifyPlayer = notifyPlayer;
            PlayerExamined = playerExamined;
        }

        protected void SetKingdomDecisionProperties(KingdomDecision kingdomDecision)
        {
            kingdomDecision.IsEnforced = IsEnforced;
            kingdomDecision.NotifyPlayer = NotifyPlayer;
            kingdomDecision.PlayerExamined = PlayerExamined;
        }


        public abstract bool TryGetKingdomDecision(IObjectManager objectManager,out KingdomDecision kingdomDecision);
    }
}
