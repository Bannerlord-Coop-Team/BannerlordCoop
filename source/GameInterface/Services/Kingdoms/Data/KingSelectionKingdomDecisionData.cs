using GameInterface.Services.ObjectManager;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;

namespace GameInterface.Services.Kingdoms.Data
{
    [ProtoContract(SkipConstructor = true)]
    public class KingSelectionKingdomDecisionData : KingdomDecisionData
    {
        [ProtoMember(1)]
        public string ClanToExclude { get; }
        public KingSelectionKingdomDecisionData(string proposedClanId, int triggerTime, bool isEnforced, bool notifyPlayer, bool playerExamined, string clanToExclude) : base(proposedClanId, triggerTime, isEnforced, notifyPlayer, playerExamined)
        {
            ClanToExclude = clanToExclude;
        }

        public override bool TryGetKingdomDecision(IObjectManager objectManager, out KingdomDecision kingdomDecision)
        {
            if (!objectManager.TryGetObject(ProposerClanId, out Clan proposerClan))
            {
                kingdomDecision = null;
                return false;
            }

            if (!objectManager.TryGetObject(ClanToExclude, out Clan clanToExclude))
            {
                kingdomDecision = null;
                return false;
            }

            kingdomDecision = new KingSelectionKingdomDecision(proposerClan, clanToExclude);
            SetKingdomDecisionProperties(kingdomDecision);
            return true;
        }
    }
}
