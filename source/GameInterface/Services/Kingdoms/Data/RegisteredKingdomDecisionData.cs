using GameInterface.Services.ObjectManager;
using ProtoBuf;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;

namespace GameInterface.Services.Kingdoms.Data
{
    /// <summary>
    /// Carries a kingdom decision of a type a mod registered through
    /// <see cref="IKingdomDecisionDataConverter.Register{TDecision}"/>: the shared decision fields plus the mod's own payload.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public class RegisteredKingdomDecisionData : KingdomDecisionData
    {
        [ProtoMember(1)]
        public string TypeName { get; }

        [ProtoMember(2)]
        public byte[] Payload { get; }

        public RegisteredKingdomDecisionData(string proposedClanId, string kingdomId, long triggerTime, bool isEnforced, bool notifyPlayer, bool playerExamined, string typeName, byte[] payload) : base(proposedClanId, kingdomId, triggerTime, isEnforced, notifyPlayer, playerExamined)
        {
            TypeName = typeName;
            Payload = payload;
        }

        /// <summary>
        /// Always false: rebuilding needs the mod's registered reader, so use
        /// <see cref="IKingdomDecisionDataConverter.TryGetKingdomDecision"/>.
        /// </summary>
        public override bool TryGetKingdomDecision(IObjectManager objectManager, out KingdomDecision kingdomDecision)
        {
            kingdomDecision = null;
            return false;
        }

        internal bool TryGetKingdomDecision(IObjectManager objectManager, Func<byte[], KingdomDecision> read, out KingdomDecision kingdomDecision)
        {
            kingdomDecision = null;
            if (!TryGetProposerClanAndKingdom(objectManager, out Clan proposerClan, out Kingdom kingdom)) return false;

            KingdomDecision decision = read(Payload ?? Array.Empty<byte>());
            if (decision == null) return false;

            SetKingdomDecisionProperties(decision, proposerClan, kingdom);
            kingdomDecision = decision;
            return true;
        }
    }
}
