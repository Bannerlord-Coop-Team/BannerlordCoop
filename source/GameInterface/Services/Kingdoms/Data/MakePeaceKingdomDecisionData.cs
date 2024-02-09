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
    public class MakePeaceKingdomDecisionData : KingdomDecisionData
    {
        private static readonly FieldInfo FactionToMakePeaceWithField = typeof(MakePeaceKingdomDecision).GetField(nameof(MakePeaceKingdomDecision.FactionToMakePeaceWith), BindingFlags.Instance | BindingFlags.Public);
        private static readonly FieldInfo DailyTributeToBePaidField = typeof(MakePeaceKingdomDecision).GetField(nameof(MakePeaceKingdomDecision.DailyTributeToBePaid), BindingFlags.Instance | BindingFlags.Public);
        private static readonly FieldInfo ApplyResultsField = typeof(MakePeaceKingdomDecision).GetField("_applyResults", BindingFlags.Instance | BindingFlags.NonPublic);

        [ProtoMember(1)]
        public string FactionToMakePeaceWithId { get; }
        [ProtoMember(2)]
        public int DailyTributeToBePaid { get; }
        [ProtoMember(3)]
        public bool ApplyResults { get; }

        public MakePeaceKingdomDecisionData(string proposedClanId, string kingdomId, long triggerTime, bool isEnforced, bool notifyPlayer, bool playerExamined, string factionToMakePeaceWithId, int dailyTributeToBePaid, bool applyResults) : base(proposedClanId, kingdomId, triggerTime, isEnforced, notifyPlayer, playerExamined)
        {
            FactionToMakePeaceWithId= factionToMakePeaceWithId;
            DailyTributeToBePaid= dailyTributeToBePaid;
            ApplyResults= applyResults;
        }

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
            ApplyResultsField.SetValue(makePeaceKingdomDecision, ApplyResults);
            kingdomDecision = makePeaceKingdomDecision;
            return true;
        }
    }
}
