using System;
using Coop.Mod.DebugUtil;
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
    ///     Represents a clientside <see cref="MobileParty" /> entity that is currently active in the game world.
    /// </summary>
    public class MobilePartyEntityClient : RailEntityClient<MobilePartyState>, IMovementHandler
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        [NotNull] private readonly IEnvironmentClient m_Environment;
        public MobilePartyEntityClient([NotNull] IEnvironmentClient environment)
        {
            m_Environment = environment;
        }

        #region IMovementHandler
        /// <summary>
        ///     Current tick of the room this entity lives in.
        /// </summary>
        public Tick Tick => Room?.Tick ?? Tick.INVALID;

        /// <summary>
        ///     Handler to issue a move command for this party to the server.
        /// </summary>
        /// <exception cref="ArgumentException"></exception>
        public void RequestMovement([NotNull] MovementData data)
        {
            if (data == null)
            {
                throw new ArgumentException(nameof(data));
            }

            if (Controller != null && Room != null)
            {
                Logger.Trace("[{tick}] Request player move entity {id} to '{position}'.", Room.Tick, Id, data);
                Room.RaiseEvent<EventPartyMoveTo>(
                    e =>
                    {
                        e.EntityId = Id;
                        e.Movement = data.ToState();
                    });
            }
        }

        /// <summary>
        ///     Requests a change of the current position of the managed party on the campaign map.
        /// </summary>
        /// <param name="position"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public void RequestPosition(Vec2 position)
        {
            throw new InvalidOperationException(
                "Client cannot set the authoritative position of a MobileParty. This is controlled by the server.");
        }
        #endregion

        public override string ToString()
        {
            return $"Party {State.PartyId} ({Id}): {m_Party}";
        }
        
        #region RailEntityClient

        /// <summary>
        ///     Called when the controller of this party changes.
        /// </summary>
        protected override void OnControllerChanged()
        {
            if (Controller != null)
                // We control the party now.
            {
                RegisterAsController();
            }
            else
            {
                UnregisterAsController();
            }
        }

        /// <summary>
        ///     Called when this party is added to the Railgun room.
        /// </summary>
        protected override void OnAdded()
        {
            State.OnPositionChanged += UpdateLocalPosition;
            State.OnMovementChanged += UpdateLocalMovement;
            State.OnPlayerControlledChanged += OnPlayerControlledChanged;
        }

        /// <summary>
        ///     Called when this party is removed from the Railgun room.
        /// </summary>
        protected override void OnRemoved()
        {
            State.OnPlayerControlledChanged -= OnPlayerControlledChanged;
            State.OnMovementChanged -= UpdateLocalMovement;
            State.OnPositionChanged -= UpdateLocalPosition;
        }

        /// <summary>
        ///     Called when this party leaves the scope of the local game instance.
        /// </summary>
        protected override void OnFrozen()
        {
            base.OnFrozen();
            if (Coop.IsArbiter)
            {
                return;
            }
            
            if (!TryGetParty(out MobileParty party))
            {
                return;
            }
            m_Environment.ScopeLeft(m_Party);
        }
        /// <summary>
        ///     Called when this party enters the scope of the local game instance.
        /// </summary>
        protected override void OnUnfrozen()
        {
            base.OnUnfrozen();
            if (Coop.IsArbiter)
            {
                return;
            }
            if (!TryGetParty(out MobileParty party))
            {
                return;
            }

            if (AuthState != null && IsValidCoordinate(AuthState.MapPosition))
            {
                // Remote controlled entity
                Vec2 moveDir = (AuthState.MapPosition.Vec2 - State.MapPosition.Vec2).Normalized();
                m_Environment.ScopeEntered(party, AuthState.MapPosition, moveDir, AuthState.Movement.ToData());
            }
            else
            {
                // We are the controller
                m_Environment.ScopeEntered(party, State.MapPosition, null, State.Movement.ToData());
            }
        }

        #endregion

        /// <summary>
        ///     Registers handlers to intercept issued movement commands to this party and send
        ///     them to the server. Should only be called for parties that are controlled by
        ///     this client.
        /// </summary>
        /// <exception cref="Exception"></exception>
        private void RegisterAsController()
        {
            if (Controller != null && State.PartyId != Guid.Empty)
            {
                if (!TryGetParty(out MobileParty controlledParty))
                {
                    throw new Exception($"Mobile party id {State.PartyId} not found. Cannot register as controller.");
                }
                m_Environment.PartySync.RegisterLocalHandler(controlledParty, this);
            }
        }

        /// <summary>
        ///     Unregisters all handlers.
        /// </summary>
        private void UnregisterAsController()
        {
            m_Environment.PartySync.Unregister(this);
        }

        /// <summary>
        ///     Handler to apply a received move command for this party.
        /// </summary>
        /// <exception cref="Exception"></exception>
        private void UpdateLocalMovement()
        {
            if (!TryGetParty(out MobileParty party))
            {
                return;
            }

            MovementData data = AuthState != null ? AuthState.Movement.ToData() : State.Movement.ToData();
            m_Environment.SetAuthoritative(party, data);
            Replay.ReplayRecording?.Invoke(Id, party, data);
        }
        /// <summary>
        ///     Handler to apply a changed position from the server to the local game state.
        /// </summary>
        private void UpdateLocalPosition()
        {
            if (!IsValidCoordinate(State.MapPosition))
            {
                return;
            }

            if (!TryGetParty(out MobileParty party))
            {
                return;
            }
            
            if (!State.IsPlayerControlled &&
                AuthState != null && IsValidCoordinate(AuthState.MapPosition) && 
                NextState != null && IsValidCoordinate(NextState.MapPosition))
            {
                // Remote controlled entity
                Vec2 moveDir = (NextState.MapPosition.Vec2 - State.MapPosition.Vec2).Normalized();
                m_Environment.SetAuthoritative(party, AuthState.MapPosition, moveDir);
            }
            else
            {
                // We are the controller
                m_Environment.SetAuthoritative(party, State.MapPosition, null);
            }
        }
        /// <summary>
        ///     Returns whether the given vector is a valid map coordinate.
        /// </summary>
        /// <param name="vec"></param>
        /// <returns></returns>
        private bool IsValidCoordinate(Vec2 vec)
        {
            return !float.IsNaN(vec.x) &&
                   !float.IsNaN(vec.y) &&
                   vec != Vec2.Zero;
        }
        
        /// <summary>
        ///     Handler to be called when the control of this party changes to or from any player.
        /// </summary>
        private void OnPlayerControlledChanged()
        {
            m_Environment.SetIsPlayerControlled(State.PartyId, State.IsPlayerControlled);
        }
        
        [CanBeNull] private MobileParty m_Party; // Do not use, call TryGetParty instead.

        /// <summary>
        ///     Returns the party for this entity.
        /// </summary>
        /// <param name="party"></param>
        /// <returns></returns>
        private bool TryGetParty(out MobileParty party)
        {
            if (m_Party == null)
            {
                m_Party = m_Environment.GetMobilePartyById(State.PartyId);
                if (m_Party == null)
                {
                    Logger.Warn("Mobile party id {PartyId} not found", State.PartyId);
                }
            }

            party = m_Party;
            return party != null;
        }
    }
}