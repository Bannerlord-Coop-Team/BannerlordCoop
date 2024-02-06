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
        private static Action<MakePeaceKingdomDecision, IFaction> SetFactionToMakePeaceWith = typeof(MakePeaceKingdomDecision).GetField(nameof(MakePeaceKingdomDecision.FactionToMakePeaceWith), BindingFlags.Instance | BindingFlags.Public).BuildUntypedSetter<MakePeaceKingdomDecision, IFaction>();
        private static Action<MakePeaceKingdomDecision, int> SetDailyTributeToBePaid = typeof(MakePeaceKingdomDecision).GetField(nameof(MakePeaceKingdomDecision.DailyTributeToBePaid), BindingFlags.Instance | BindingFlags.Public).BuildUntypedSetter<MakePeaceKingdomDecision, int>();
        private static Action<MakePeaceKingdomDecision, bool> SetApplyResults = typeof(MakePeaceKingdomDecision).GetField("_applyResults", BindingFlags.Instance | BindingFlags.NonPublic).BuildUntypedSetter<MakePeaceKingdomDecision, bool>();

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
                !objectManager.TryGetObject(FactionToMakePeaceWithId, out MBObjectBase factionToMakePeaceWith) ||
                !(factionToMakePeaceWith is IFaction))
            {
                kingdomDecision = null;
                return false;
            }

            MakePeaceKingdomDecision makePeaceKingdomDecision = (MakePeaceKingdomDecision)FormatterServices.GetUninitializedObject(typeof(MakePeaceKingdomDecision));
            SetKingdomDecisionProperties(makePeaceKingdomDecision, proposerClan, kingdom);
            SetFactionToMakePeaceWith(makePeaceKingdomDecision, (IFaction)factionToMakePeaceWith);
            SetDailyTributeToBePaid(makePeaceKingdomDecision, DailyTributeToBePaid);
            SetApplyResults(makePeaceKingdomDecision, ApplyResults);
            kingdomDecision = makePeaceKingdomDecision;
            return true;
        }
    }
}
