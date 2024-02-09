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
    /// <summary>
    /// Class for serializing <see cref="KingSelectionKingdomDecision"> class.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public class KingSelectionKingdomDecisionData : KingdomDecisionData
    {
        private static Action<KingSelectionKingdomDecision, Clan> SetClanToExclude = typeof(KingSelectionKingdomDecision).GetField("_clanToExclude", BindingFlags.Instance | BindingFlags.NonPublic).BuildUntypedSetter<KingSelectionKingdomDecision, Clan>();

        [ProtoMember(1)]
        public string ClanToExcludeId { get; }
        public KingSelectionKingdomDecisionData(string proposedClanId, string kingdomId, long triggerTime, bool isEnforced, bool notifyPlayer, bool playerExamined, string clanToExcludeId) : base(proposedClanId, kingdomId, triggerTime, isEnforced, notifyPlayer, playerExamined)
        {
            ClanToExcludeId = clanToExcludeId;
        }

        /// <inheritdoc/>
        public override bool TryGetKingdomDecision(IObjectManager objectManager, out KingdomDecision kingdomDecision)
        {
            if (!TryGetProposerClanAndKingdom(objectManager, out Clan proposerClan, out Kingdom kingdom) ||
                !objectManager.TryGetObject(ClanToExcludeId, out Clan clanToExclude))
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
