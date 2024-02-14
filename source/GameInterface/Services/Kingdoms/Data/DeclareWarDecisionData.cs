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
    /// <summary>
    /// Class for serializing <see cref="DeclareWarDecision"> class.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public class DeclareWarDecisionData : KingdomDecisionData
    {
        private static readonly FieldInfo FactionToDeclareWarOnField = typeof(DeclareWarDecision).GetField(nameof(DeclareWarDecision.FactionToDeclareWarOn), BindingFlags.Instance | BindingFlags.Public);

        [ProtoMember(1)]
        public string FactionToDeclareWarOnId { get; }

        public DeclareWarDecisionData(string proposedClanId, string kingdomId, long triggerTime, bool isEnforced, bool notifyPlayer, bool playerExamined, string factionToDeclareWarOnId) :base(proposedClanId, kingdomId, triggerTime, isEnforced, notifyPlayer, playerExamined)
        {
            FactionToDeclareWarOnId = factionToDeclareWarOnId;
        }

        /// <inheritdoc/>
        public override bool TryGetKingdomDecision(IObjectManager objectManager,out KingdomDecision kingdomDecision)
        {
            if (!TryGetProposerClanAndKingdom(objectManager, out Clan proposerClan, out Kingdom kingdom) ||
                (!objectManager.TryGetObject(FactionToDeclareWarOnId, out Kingdom factionKingdomToDeclareWarOn) &
                !objectManager.TryGetObject(FactionToDeclareWarOnId, out Clan factionClanToDeclareWarOn)))
            {
                kingdomDecision = null;
                return false;
            }

            IFaction faction;
            if (factionKingdomToDeclareWarOn != null)
            {
                faction = factionKingdomToDeclareWarOn;
            }
            else
            {
                faction = factionClanToDeclareWarOn;
            }

            DeclareWarDecision declareWarDecision = (DeclareWarDecision)FormatterServices.GetUninitializedObject(typeof(DeclareWarDecision));
            SetKingdomDecisionProperties(declareWarDecision, proposerClan, kingdom);
            FactionToDeclareWarOnField.SetValue(declareWarDecision, faction);
            kingdomDecision = declareWarDecision;
            return true;
        }
    }
}
