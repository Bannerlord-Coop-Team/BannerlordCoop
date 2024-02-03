using GameInterface.Services.Kingdoms.Data.IFactionDatas;
using GameInterface.Services.ObjectManager;
using ProtoBuf;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;

namespace GameInterface.Services.Kingdoms.Data
{
    [ProtoContract(SkipConstructor = true)]
    public class MakePeaceKingdomDecisionData : KingdomDecisionData
    {
        [ProtoMember(1)]
        public IFactionData FactionToMakePeaceWith { get; }
        [ProtoMember(2)]
        public int DailyTributeToBePaid { get; }
        [ProtoMember(3)]
        public bool ApplyResults { get; }

        public MakePeaceKingdomDecisionData(string proposedClanId, int triggerTime, bool isEnforced, bool notifyPlayer, bool playerExamined, IFactionData factionToMakePeaceWith, int dailyTributeToBePaid, bool applyResults) : base(proposedClanId, triggerTime, isEnforced, notifyPlayer, playerExamined)
        {
            FactionToMakePeaceWith= factionToMakePeaceWith;
            DailyTributeToBePaid= dailyTributeToBePaid;
            ApplyResults= applyResults;
        }

        public override bool TryGetKingdomDecision(IObjectManager objectManager, out KingdomDecision kingdomDecision)
        {
            if (!objectManager.TryGetObject(ProposerClanId, out Clan clan))
            {
                kingdomDecision = null;
                return false;
            }

            if (!FactionToMakePeaceWith.TryGetIFaction(objectManager, out IFaction factionToMakePeaceWith))
            {
                kingdomDecision = null;
                return false;
            }

            kingdomDecision = new MakePeaceKingdomDecision(clan, factionToMakePeaceWith, DailyTributeToBePaid, ApplyResults);
            SetKingdomDecisionProperties(kingdomDecision);
            return true;
        }
    }
}
