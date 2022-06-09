using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Common;
using Coop.Mod.Persistence;
using Coop.Mod.Persistence.Party;
using Coop.Mod.Scope;
using CoopFramework;
using HarmonyLib;
using JetBrains.Annotations;
using NLog;
using Sync.Behaviour;
using Sync.Call;
using Sync.Value;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.GameSync.Party
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
    ///         <see cref="MobilePartyMovementSync"/> instance which in turn decides what to do with the captured data.
    /// 
    ///     2.  Position data. This is the 2D position on the campaign map of the party. It is captured into a buffer
    ///         on the server only. The positions captured on the server will be sent to all clients. This will overwrite
    ///         whatever the client is doing locally and acts as a ground truth for all parties, regardless of movement
    ///         orders.
    /// </summary>
    public class CampaignMapMovement
    {
        #region static patch data
        /// <summary>
        ///     Field reference for the backing field of "DefaultBehaviourNeedsUpdate".
        /// </summary>
        private static readonly AccessTools.FieldRef<MobileParty, bool> DefaultBehaviorNeedsUpdate =
            AccessTools.FieldRefAccess<MobileParty, bool>("_defaultBehaviorNeedsUpdate");
        #endregion

        internal CampaignMapMovement([NotNull] MapMovementPatches patches)
        {
            m_Patches = patches;
        }
        public void SetMovement(MobileParty party, MovementData data)
        {
            m_Patches.MovementOrderGroup.SetTyped(party, data);
            DefaultBehaviorNeedsUpdate(party) = Coop.IsServer;

            if (party.IsRemotePlayerMainParty())
            // That is a remote player moving. We need to update the local MainParty as well
            // because Campaign.Tick will otherwise not update the AI decisions and just
            // ignore some actions (for example EngageParty).
            {
                DefaultBehaviorNeedsUpdate(Campaign.Current.MainParty) = true;
            }
            party.Ai.SetDoNotMakeNewDecisions(true);
            party.ValidateSpeed();
        }
        public void SetPosition(MobileParty party, Vec2 position, Vec2? facingDirection)
        {
            m_NextPosition = position;
            m_FacingDirection = facingDirection;
        }

        public void ApplyAuthoritativeState(MobileParty party)
        {
            if (!party.IsInClientScope())
            {
                return;
            }

            const float fAllowedLocalOffsetPlayer = 1.0f;
            const float fAllowedLocalOffsetNPC = 0.0001f;
            if (m_NextPosition.IsValid &&
                !Compare.CoordinatesEqual(party.Position2D, m_NextPosition))
            {
                float fDistSqr = m_NextPosition.DistanceSquared(party.Position2D);
                float fDistSqrAllowed = party.IsAnyPlayerMainParty() ? fAllowedLocalOffsetPlayer : fAllowedLocalOffsetNPC;
                fDistSqrAllowed *= fDistSqrAllowed;
                if (fDistSqr > fDistSqrAllowed)
                {
                    m_Patches.MapPositionSetter.Invoke(EOriginator.RemoteAuthority, party, new object[] { m_NextPosition });
                }
            }

            bool extrapolateFacingDirection = party != MobileParty.MainParty;
            if (extrapolateFacingDirection && m_FacingDirection.HasValue && m_FacingDirection.Value != Vec2.Zero)
            {
                // Remote controlled instance. Update the movement command to give the appearance that party actually has an objective...
                Vec2 predictedPos = m_NextPosition + m_FacingDirection.Value * 1f;
                m_Patches.MovementOrderGroup.SetTyped(party, new MovementData()
                {
                    DefaultBehaviour = AiBehavior.GoToPoint,
                    TargetParty = null,
                    TargetSettlement = null,
                    TargetPosition = predictedPos
                });
                DefaultBehaviorNeedsUpdate(party) = Coop.IsServer;

                if (party.IsRemotePlayerMainParty())
                // That is a remote player moving. We need to update the local MainParty as well
                // because Campaign.Tick will otherwise not update the AI decisions and just
                // ignore some actions (for example EngageParty).
                {
                    DefaultBehaviorNeedsUpdate(Campaign.Current.MainParty) = true;
                }
                party.Ai.SetDoNotMakeNewDecisions(true);
                party.ValidateSpeed();
            }
        }

        private MapMovementPatches m_Patches;
        private Vec2 m_NextPosition = Vec2.Invalid;
        private Vec2? m_FacingDirection = null;
        private static readonly NLog.Logger Logger = LogManager.GetCurrentClassLogger();
    }
}
