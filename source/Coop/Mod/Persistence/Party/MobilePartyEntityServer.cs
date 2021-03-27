using System;
using JetBrains.Annotations;
using NLog;
using RailgunNet.Logic;
using RailgunNet.System.Types;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using Logger = NLog.Logger;

namespace Coop.Mod.Persistence.Party
{
    /// <summary>
    ///     Represents a serverside <see cref="MobileParty" /> entity that is currently active in the game world.
    /// </summary>
    public class MobilePartyEntityServer : RailEntityServer<MobilePartyState>, IMovementHandler
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        [NotNull] private readonly IEnvironmentServer m_Environment;
        private bool m_bIsRegisteredAsController;
        [CanBeNull] private MobileParty m_Instance;

        public MobilePartyEntityServer([NotNull] IEnvironmentServer environment)
        {
            m_Environment = environment;
        }

        public override string ToString()
        {
            return $"Party {State.PartyId} ({Id}): {m_Instance}";
        }

        /// <summary>
        ///     Registers handlers to intercept issued movement commands to this party and apply them
        ///     the authoritative state.
        /// </summary>
        private void RegisterAsDefaultController([NotNull] MobileParty party)
        {
            if (IsRemoving)
            {
                // Will be removed from the room in the next tick. There's no point in taking control over it.
                return;
            }

            bool bIsControlledByAnyClient = Controller != null;
            if (bIsControlledByAnyClient ||
                m_bIsRegisteredAsController)
            {
                return;
            }

            State.IsPlayerControlled = false;
            m_Environment.PartySync.RegisterLocalHandler(party, this);
            m_bIsRegisteredAsController = true;
        }

        /// <summary>
        ///     Unregisters all handlers.
        /// </summary>
        private void UnregisterAsController()
        {
            m_Environment.PartySync.Unregister(this);
            m_bIsRegisteredAsController = false;
        }

        /// <summary>
        ///     The range this party should be able to see (as in: receive sync updates for) other parties. 
        /// </summary>
        public float ScopeRange => 5f;    // TODO: consider party stats

        #region IMovementHandler

        public Tick Tick => Room?.Tick ?? Tick.INVALID;

        /// <summary>
        ///     Handler to apply a movement command to the authoritative state.
        /// </summary>
        /// <param name="currentPosition"></param>
        /// <param name="data"></param>
        /// <exception cref="ArgumentException"></exception>
        public void RequestMovement(MovementData data)
        {
            Logger.Trace(
                "[{tick}] Server controlled entity move {id} to '{position}'.",
                Room.Tick,
                Id,
                data);

            State.Movement.DefaultBehavior = data.DefaultBehaviour;
            State.Movement.TargetPosition = data.TargetPosition;
            State.Movement.TargetPartyIndex = data.TargetParty?.Id ?? Coop.InvalidId;
            State.Movement.SettlementIndex =
                data.TargetSettlement?.Id ?? Coop.InvalidId;
        }

        /// <summary>
        ///     Requests a change of the current position of the managed party on the campaign map.
        /// </summary>
        /// <param name="position"></param>
        public void RequestPosition(Vec2 position)
        {
            State.MapPosition = position;
        }

        #endregion

        #region RailEntityServer

        /// <summary>
        ///     Called when the controller of this party changes.
        /// </summary>
        protected override void OnControllerChanged()
        {
            if (Controller == null &&
                m_Instance != null)
            {
                RegisterAsDefaultController(m_Instance);
            }
            else
            {
                UnregisterAsController();
                State.IsPlayerControlled = true;
            }
        }

        /// <summary>
        ///     Called when this party is added to the room.
        /// </summary>
        protected override void OnAdded()
        {
            // Get the instance
            m_Instance = m_Environment.GetMobilePartyById(State.PartyId);
            if (m_Instance == null)
            {
                Logger.Warn(
                    "Mobile party id {PartyId} not found in the local game state. Desync!",
                    State.PartyId);
                return;
            }

            // Server takes initial control of all entities. Players are then granted control over their own
            // party.
            RegisterAsDefaultController(m_Instance);

            // Get initial state from the game object.
            MovementData movement = null;
            Vec2? position = null;
            // TODO: this should really be done in the main thread, but it is currently not really feasible because
            //       every party gets added individually to RailGun. This would result in thousands of separate calls
            //       to this function, each waiting for the main game loop. This introduces seconds (!) of lag when
            //       first unpausing the game. Fixed with the dedicated server.
            // GameLoopRunner.RunOnMainThread(() =>
            // {
                movement = m_Instance.GetMovementData();
                position = m_Instance.Position2D;
            // });
            RequestMovement(movement);
            RequestPosition(position.Value);
        }

        /// <summary>
        ///     Called when this party is removed from the room.
        /// </summary>
        protected override void OnRemoved()
        {
            UnregisterAsController();
        }

        #endregion
    }
}