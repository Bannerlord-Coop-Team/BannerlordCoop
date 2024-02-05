using Common.Extensions;
using GameInterface.Services.Kingdoms.Data.IFactionDatas;
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
    public class DeclareWarDecisionData : KingdomDecisionData
    {
        private static Action<DeclareWarDecision, IFaction> SetFactionToDeclareWarOn = typeof(DeclareWarDecision).GetField(nameof(DeclareWarDecision.FactionToDeclareWarOn), BindingFlags.Instance | BindingFlags.Public).BuildUntypedSetter<DeclareWarDecision, IFaction>();

        [ProtoMember(1)]
        public IFactionData FactionToDeclareWarOn { get; }

        public DeclareWarDecisionData(string proposedClanId, string kingdomId, long triggerTime, bool isEnforced, bool notifyPlayer, bool playerExamined, IFactionData factionToDeclareWarOn) :base(proposedClanId, kingdomId, triggerTime, isEnforced, notifyPlayer, playerExamined)
        {
            FactionToDeclareWarOn = factionToDeclareWarOn;
        }

        public override bool TryGetKingdomDecision(IObjectManager objectManager,out KingdomDecision kingdomDecision)
        {
            if (!TryGetProposerClanAndKingdom(objectManager, out Clan proposerClan, out Kingdom kingdom) ||
                !FactionToDeclareWarOn.TryGetIFaction(objectManager, out IFaction factionToDeclareWarOn))
            {
                kingdomDecision = null;
                return false;
            }

            DeclareWarDecision declareWarDecision = (DeclareWarDecision)FormatterServices.GetUninitializedObject(typeof(DeclareWarDecision));
            SetKingdomDecisionProperties(declareWarDecision, proposerClan, kingdom);
            SetFactionToDeclareWarOn(declareWarDecision, factionToDeclareWarOn);
            kingdomDecision = declareWarDecision;
            return true;
        }
    }
}
