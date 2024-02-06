using Common.Extensions;
using GameInterface.Services.ObjectManager;
using ProtoBuf;
using System;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Kingdoms.Data
{
    [ProtoContract(SkipConstructor = true)]
    public class DeclareWarDecisionData : KingdomDecisionData
    {
        private static Action<DeclareWarDecision, IFaction> SetFactionToDeclareWarOn = typeof(DeclareWarDecision).GetField(nameof(DeclareWarDecision.FactionToDeclareWarOn), BindingFlags.Instance | BindingFlags.Public).BuildUntypedSetter<DeclareWarDecision, IFaction>();

        [ProtoMember(1)]
        public string FactionToDeclareWarOnId { get; }

        public DeclareWarDecisionData(string proposedClanId, string kingdomId, long triggerTime, bool isEnforced, bool notifyPlayer, bool playerExamined, string factionToDeclareWarOnId) :base(proposedClanId, kingdomId, triggerTime, isEnforced, notifyPlayer, playerExamined)
        {
            FactionToDeclareWarOnId = factionToDeclareWarOnId;
        }

        public override bool TryGetKingdomDecision(IObjectManager objectManager,out KingdomDecision kingdomDecision)
        {
            if (!TryGetProposerClanAndKingdom(objectManager, out Clan proposerClan, out Kingdom kingdom) ||
                !objectManager.TryGetObject(FactionToDeclareWarOnId, out MBObjectBase factionToDeclareWarOn) ||
                !(factionToDeclareWarOn is IFaction))
            {
                kingdomDecision = null;
                return false;
            }

            DeclareWarDecision declareWarDecision = (DeclareWarDecision)FormatterServices.GetUninitializedObject(typeof(DeclareWarDecision));
            SetKingdomDecisionProperties(declareWarDecision, proposerClan, kingdom);
            SetFactionToDeclareWarOn(declareWarDecision, (IFaction)factionToDeclareWarOn);
            kingdomDecision = declareWarDecision;
            return true;
        }
    }
}
