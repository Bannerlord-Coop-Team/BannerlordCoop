using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Common;
using Coop.Mod.Persistence;
using Coop.Mod.Persistence.Party;
using CoopFramework;
using HarmonyLib;
using JetBrains.Annotations;
using NLog;
using Sync.Behaviour;
using Sync.Call;
using Sync.Value;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Patch.MobilePartyPatches
{
    /// <summary>
    ///     Handles synchronization of campaign map movement data of a <see cref="MobileParty"/> instance. It records
    ///     all changes made to the relevant fields during one frame. At the end of a frame, the changes are accumulated
    ///     and sent to all clients. Two categories of fields are synchronized this way:
    ///
    ///     1.  Movement order data. See <see cref="MovementData"/>. These are different fields of a party that are
    ///         relevant to it's movement behaviour on the map. Specifically it contains the <see cref="AiBehavior"/>
    ///         that defines the "long term" movement goal and all its related information.
    ///
    ///         The movement order data is buffered for each frame. The buffered data is then passed onto the
    ///         <see cref="MobilePartySync"/> instance which in turn decides what to do with the captured data.
    /// 
    ///     2.  Position data. This is the 2D position on the campaign map of the party. It is captured into a buffer
    ///         on the server only. The positions captured on the server will be sent to all clients. This will overwrite
    ///         whatever the client is doing locally and acts as a ground truth for all parties, regardless of movement
    ///         orders.
    /// </summary>
    public class CampaignMapMovement : CoopManaged<CampaignMapMovement, MobileParty>, IDisposable
    {
        #region static patch data
        /// <summary>
        ///     Definition of the patch.
        /// </summary>
        static CampaignMapMovement()
        {
            // Define the patched fields for target movement & current position
            MovementOrderGroup =
                new FieldAccessGroup<MobileParty, MovementData>(new FieldAccess[] 
                {
                    Field<AiBehavior>("_defaultBehavior"),
                    Field<Settlement>("_targetSettlement"),
                    Field<MobileParty>("_targetParty"),
                    Field<Vec2>("_targetPosition"),
                    Field<int>("_numberOfFleeingsAtLastTravel")
                });
            MapPosition = Field<Vec2>("_position2D");
            MapPositionSetter = Setter(nameof(MobileParty.Position2D));
            DefaultBehaviourSetter = Setter(nameof(MobileParty.DefaultBehavior));
            TargetSettlementSetter = Setter(nameof(MobileParty.TargetSettlement));
            TargetPartySetter = Setter(nameof(MobileParty.TargetParty));
            TargetPositionSetter = Setter(nameof(MobileParty.TargetPosition));
            TickAi = Method("TickAi");
            
            Sync = new MobilePartySync(MovementOrderGroup, MapPosition);

            // On clients, send the movement orders for our party to the server
            // When(GameLoop & !CoopConditions.IsServer & CoopConditions.ControlsParty)
            //     .Changes(MovementOrderGroup)
            //     .Through(
            //         DefaultBehaviourSetter,
            //         TargetSettlementSetter,
            //         TargetPartySetter,
            //         TargetPositionSetter)
            //     .Broadcast(() => Sync);

            // Movement orders are only applied on the server and for the controlled parties.
            When(GameLoop & !CoopConditions.ControlsParty)
                .Changes(MovementOrderGroup)
                .Through(
                    DefaultBehaviourSetter,
                    TargetSettlementSetter,
                    TargetPartySetter,
                    TargetPositionSetter)
                .Revert();            

            AutoWrapAllInstances(party => new CampaignMapMovement(party));
        }

        #region Patched members
        /// <summary>
        ///     Synchronization instance for all movement data.
        /// </summary>
        public static MobilePartySync Sync { get; }
        /// <summary>
        ///     Field access group for all movement related data.
        /// </summary>
        private static FieldAccessGroup<MobileParty, MovementData> MovementOrderGroup { get; }
        /// <summary>
        ///     Field access for the position on the campaign map.
        /// </summary>
        public static FieldAccess<MobileParty, Vec2> MapPosition { get; }
        public static PatchedInvokable MapPositionSetter { get; }
        public static PatchedInvokable TargetPositionSetter { get; }
        public static PatchedInvokable TargetPartySetter { get; }
        public static PatchedInvokable TargetSettlementSetter { get; }
        public static PatchedInvokable DefaultBehaviourSetter { get; }
        public static PatchedInvokable TickAi { get; }
        #endregion
        /// <summary>
        ///     Field reference for the backing field of "DefaultBehaviourNeedsUpdate".
        /// </summary>
        private static readonly AccessTools.FieldRef<MobileParty, bool> DefaultBehaviorNeedsUpdate =
            AccessTools.FieldRefAccess<MobileParty, bool>("_defaultBehaviorNeedsUpdate");
        #endregion
        /// <summary>
        ///     Applies the authoritative state to the local game state.
        /// </summary>
        public static void ApplyAuthoritativeState()
        {
            foreach (var item in Instances)
            {
                MobileParty party = CoopObjectManager.GetObject(item.Key) as MobileParty;
                if (party != null)
                {
                    item.Value.ApplyServersideState(party);
                }
            }
        }
        
        public static void SetMovement(MobileParty party, MovementData data)
        {
            MovementOrderGroup.SetTyped(party, data);
            DefaultBehaviorNeedsUpdate(party) = Coop.IsServer;

            if (party.IsRemotePlayerMainParty())
            // That is a remote player moving. We need to update the local MainParty as well
            // because Campaign.Tick will otherwise not update the AI decisions and just
            // ignore some actions (for example EngageParty).
            {
                DefaultBehaviorNeedsUpdate(Campaign.Current.MainParty) = true;
            }
            party.Ai.SetDoNotMakeNewDecisions(true);
            party.ComputeSpeed();
        }

        /// <summary>
        ///     All created <see cref="CampaignMapMovement"/> instances sorted by the <see cref="MBGUID"/> of the
        ///     managed party.
        /// </summary>
        public static Dictionary<Guid, CampaignMapMovement> Instances = new Dictionary<Guid, CampaignMapMovement>();

        /// <summary>
        ///     Callback when the world map position of a mobile party was changed by the server.
        /// </summary>
        /// <param name="party"></param>
        /// <param name="posCurrent"></param>
        /// <param name="facingDirection"></param>
        public static void RemoteMapPositionChanged(MobileParty party, Vec2 posCurrent, Vec2? facingDirection)
        {
            Guid partyGuid = CoopObjectManager.GetGuid(party);

            if (Instances.TryGetValue(partyGuid, out CampaignMapMovement wrapper))
            {
                wrapper.SetPosition(party, posCurrent, facingDirection);
            }
        }
        

        #region Instance
        public CampaignMapMovement([NotNull] MobileParty instance) : base(instance)
        {
            Guid partyGuid = CoopObjectManager.GetGuid(instance);

            Instances[partyGuid] = this;
            if (!Coop.IsController(instance))
            {
                // Disable AI decision making for newly spawned parties that we do not control locally. Will be kept
                // intact by a separate patch DisablePartyDecisionMaking.
                instance.Ai.SetDoNotMakeNewDecisions(true);
            }
        }
        public void Dispose()
        {
            var entry = Instances.Where(item => item.Value == this).ToList();
            foreach (var item in entry)
            {
                Instances.Remove(item.Key);
            }
        }
        private void SetMovementGoal(MobileParty party, MovementData data)
        {
            m_TargetMovementData = data;
            ApplyServersideState(party);
        }
        private void SetPosition(MobileParty party, Vec2 position, Vec2? facingDirection)
        {
            m_NextPosition = position;
            m_FacingDirection = facingDirection;
            ApplyServersideState(party);
        }

        private void ApplyServersideState(MobileParty party)
        {
            if (Coop.IsArbiter &&
                !party.IsAnyPlayerMainParty())
            {
                // The arbiter only needs to update player parties
                return;
            }
            
            const float fAllowedLocalOffsetPlayer = 2.0f;
            const float fAllowedLocalOffsetNPC = 0.0001f;
            if (m_NextPosition.IsValid &&
                !Compare.CoordinatesEqual(party.Position2D, m_NextPosition))
            {
                float fDistSqr = m_NextPosition.DistanceSquared(party.Position2D);
                float fDistSqrAllowed = party.IsAnyPlayerMainParty() ? fAllowedLocalOffsetPlayer : fAllowedLocalOffsetNPC;
                fDistSqrAllowed *= fDistSqrAllowed;
                if (fDistSqr > fDistSqrAllowed)
                {
                     MapPositionSetter.Invoke(EOriginator.RemoteAuthority, party, new object[] {m_NextPosition});
                }

                if (m_FacingDirection.HasValue && m_FacingDirection.Value != Vec2.Zero)
                {
                    Vec2 predictedPos = m_NextPosition + (m_FacingDirection.Value * 5f);
                    m_TargetMovementData = new MovementData()
                    {
                        DefaultBehaviour = AiBehavior.GoToPoint,
                        TargetParty = null,
                        TargetSettlement = null,
                        TargetPosition = predictedPos
                    };
                }
            }

            if (m_TargetMovementData == null || !m_TargetMovementData.IsValid())
            {
                return;
            }
            
            MovementData currentMovementData = party.GetMovementData();
            if (!currentMovementData.Equals(m_TargetMovementData))
            {
                MovementOrderGroup.SetTyped(party, m_TargetMovementData);
                DefaultBehaviorNeedsUpdate(party) = Coop.IsServer;

                if (party.IsRemotePlayerMainParty())
                    // That is a remote player moving. We need to update the local MainParty as well
                    // because Campaign.Tick will otherwise not update the AI decisions and just
                    // ignore some actions (for example EngageParty).
                {
                    DefaultBehaviorNeedsUpdate(Campaign.Current.MainParty) = true;
                }
                party.Ai.SetDoNotMakeNewDecisions(true);
                party.ComputeSpeed();
            }
        }

        public override string ToString()
        {
            bool instanceAlive = TryGetInstance(out MobileParty managed);
            string instance = instanceAlive ? managed.ToString() : "expired_instance";
            return $"{base.ToString()}: {instance}";
        }

        private MovementData m_TargetMovementData;
        private Vec2 m_NextPosition = Vec2.Invalid;
        private Vec2? m_FacingDirection = null;
        private static readonly NLog.Logger Logger = LogManager.GetCurrentClassLogger();

        #endregion
    }
}
