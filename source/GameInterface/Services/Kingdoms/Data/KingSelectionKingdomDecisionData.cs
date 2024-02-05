using Common.Extensions;
using GameInterface.Services.ObjectManager;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;

namespace GameInterface.Services.Kingdoms.Data
{
    [ProtoContract(SkipConstructor = true)]
    public class KingSelectionKingdomDecisionData : KingdomDecisionData
    {
        private static Action<KingSelectionKingdomDecision, Clan> SetClanToExclude = typeof(KingSelectionKingdomDecision).GetField("_clanToExclude", BindingFlags.Instance | BindingFlags.NonPublic).BuildUntypedSetter<KingSelectionKingdomDecision, Clan>();

        [ProtoMember(1)]
        public string ClanToExclude { get; }
        public KingSelectionKingdomDecisionData(string proposedClanId, string kingdomId, long triggerTime, bool isEnforced, bool notifyPlayer, bool playerExamined, string clanToExclude) : base(proposedClanId, kingdomId, triggerTime, isEnforced, notifyPlayer, playerExamined)
        {
            ClanToExclude = clanToExclude;
        }

        public override bool TryGetKingdomDecision(IObjectManager objectManager, out KingdomDecision kingdomDecision)
        {
            if (!TryGetProposerClanAndKingdom(objectManager, out Clan proposerClan, out Kingdom kingdom) ||
                !objectManager.TryGetObject(ClanToExclude, out Clan clanToExclude))
            {
                kingdomDecision = null;
                return false;
            }

            KingSelectionKingdomDecision kingSelectionKingdomDecision = (KingSelectionKingdomDecision)FormatterServices.GetUninitializedObject(typeof(KingSelectionKingdomDecision));
            SetKingdomDecisionProperties(kingSelectionKingdomDecision, proposerClan, kingdom);
            SetClanToExclude(kingSelectionKingdomDecision, clanToExclude);
            kingdomDecision = kingSelectionKingdomDecision;
            return true;
        }
    }
}
