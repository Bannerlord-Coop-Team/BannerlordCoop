using GameInterface.Services.ObjectManager;
using ProtoBuf;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;

namespace GameInterface.Services.Kingdoms.Data
{
    /// <summary>
    /// Class for serializing <see cref="MakePeaceKingdomDecision"> class.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public class MakePeaceKingdomDecisionData : KingdomDecisionData
    {
        private static readonly FieldInfo FactionToMakePeaceWithField = typeof(MakePeaceKingdomDecision).GetField(nameof(MakePeaceKingdomDecision.FactionToMakePeaceWith), BindingFlags.Instance | BindingFlags.Public);
        private static readonly FieldInfo DailyTributeToBePaidField = typeof(MakePeaceKingdomDecision).GetField(nameof(MakePeaceKingdomDecision.DailyTributeToBePaid), BindingFlags.Instance | BindingFlags.Public);
        private static readonly FieldInfo DailyTributeDurationInDaysField = typeof(MakePeaceKingdomDecision).GetField(nameof(MakePeaceKingdomDecision.DailyTributeDurationInDays), BindingFlags.Instance | BindingFlags.Public);
        private static readonly FieldInfo ApplyResultsField = typeof(MakePeaceKingdomDecision).GetField("_applyResults", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo IsProposedByOpponentField = typeof(MakePeaceKingdomDecision).GetField("_isProposedByOpponent", BindingFlags.Instance | BindingFlags.NonPublic);

        [ProtoMember(1)]
        public string FactionToMakePeaceWithId { get; }
        [ProtoMember(2)]
        public int DailyTributeToBePaid { get; }
        [ProtoMember(3)]
        public bool ApplyResults { get; }
        [ProtoMember(4)]
        public int DailyTributeDurationInDays { get; }
        [ProtoMember(5)]
        public bool IsProposedByOpponent { get; }

        public MakePeaceKingdomDecisionData(string proposedClanId, string kingdomId, long triggerTime, bool isEnforced, bool notifyPlayer, bool playerExamined, string factionToMakePeaceWithId, int dailyTributeToBePaid, bool applyResults, int dailyTributeDurationInDays, bool isProposedByOpponent) : base(proposedClanId, kingdomId, triggerTime, isEnforced, notifyPlayer, playerExamined)
        {
            FactionToMakePeaceWithId = factionToMakePeaceWithId;
            DailyTributeToBePaid = dailyTributeToBePaid;
            ApplyResults = applyResults;
            DailyTributeDurationInDays = dailyTributeDurationInDays;
            IsProposedByOpponent = isProposedByOpponent;
        }

        /// <inheritdoc/>
        public override bool TryGetKingdomDecision(IObjectManager objectManager, out KingdomDecision kingdomDecision)
        {
            if (!TryGetProposerClanAndKingdom(objectManager, out Clan proposerClan, out Kingdom kingdom) ||
                (!objectManager.TryGetObject(FactionToMakePeaceWithId, out Kingdom factionKingdomToMakePeaceWith) &
                !objectManager.TryGetObject(FactionToMakePeaceWithId, out Clan factionClanToMakePeaceWith)))
            {
                kingdomDecision = null;
                return false;
            }

            IFaction faction;
            if (factionKingdomToMakePeaceWith != null)
            {
                faction = factionKingdomToMakePeaceWith;
            }
            else
            {
                faction = factionClanToMakePeaceWith;
            }

            MakePeaceKingdomDecision makePeaceKingdomDecision = (MakePeaceKingdomDecision)FormatterServices.GetUninitializedObject(typeof(MakePeaceKingdomDecision));
            SetKingdomDecisionProperties(makePeaceKingdomDecision, proposerClan, kingdom);
            FactionToMakePeaceWithField.SetValue(makePeaceKingdomDecision, faction);
            DailyTributeToBePaidField.SetValue(makePeaceKingdomDecision, DailyTributeToBePaid);
            DailyTributeDurationInDaysField.SetValue(makePeaceKingdomDecision, DailyTributeDurationInDays);
            ApplyResultsField.SetValue(makePeaceKingdomDecision, ApplyResults);
            IsProposedByOpponentField.SetValue(makePeaceKingdomDecision, IsProposedByOpponent);
            kingdomDecision = makePeaceKingdomDecision;
            return true;
        }
    }
}
