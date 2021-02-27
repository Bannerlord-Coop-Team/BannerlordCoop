using System;
using System.Linq;
using Coop.Mod.DebugUtil;
using JetBrains.Annotations;
using NLog;
using RailgunNet.Logic;
using RailgunNet.System.Types;
using RemoteAction;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using Logger = NLog.Logger;

namespace Coop.Mod.Persistence.Party
{
    /// <summary>
    ///     Railgun: Mobile party implementation for clients. One instance for each mobile party
    ///     that is registered in the Railgun room.
    /// </summary>
    public class MobilePartyEntityClient : RailEntityClient<MobilePartyState>, IMovementHandler
    {
        public MobilePartyEntityClient([NotNull] IEnvironmentClient environment)
        {
            m_Environment = environment;
        }

        public override string ToString()
        {
            return $"Party {State.PartyId} ({Id}): {m_ManagedParty}";
        }
        
        /// <summary>
        ///     Current tick of the room this entity lives in.
        /// </summary>
        public Tick Tick => Room?.Tick ?? Tick.INVALID;

        /// <summary>
        ///     Handler to issue a move command for this party to the server.
        /// </summary>
        /// <param name="currentPosition"></param>
        /// <exception cref="ArgumentException"></exception>
        public void RequestMovement(Vec2 currentPosition, [NotNull] MovementData data)
        {
            if (data == null)
            {
                throw new ArgumentException(nameof(data));
            }

            Logger.Trace("[{tick}] Request move entity {id} to '{position}'.", Room.Tick, Id, data);
            Room.RaiseEvent<EventPartyMoveTo>(
                e =>
                {
                    e.EntityId = Id;
                    e.Movement = data.ToState();
                });
        }
        
        /// <summary>
        ///     Called when the controller of this party changes.
        /// </summary>
        protected override void OnControllerChanged()
        {
            if (Controller != null)
            {
                // We control the party now.
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
        ///     Registers handlers to intercept issued movement commands to this party and send
        ///     them to the server. Should only be called for parties that are controlled by
        ///     this client.
        /// </summary>
        /// <exception cref="Exception"></exception>
        private void RegisterAsController()
        {
            if (m_ManagedParty == null && Controller != null)
            {
                m_ManagedParty = m_Environment.GetMobilePartyById(State.PartyId);
                if (m_ManagedParty == null)
                {
                    throw new Exception($"Mobile party id {State.PartyId} not found.");
                }
                m_Environment.PartySync.RegisterLocalHandler(m_ManagedParty, this);
            }
        }

        /// <summary>
        ///     Unregisters all handlers.
        /// </summary>
        private void UnregisterAsController()
        {
            m_Environment.PartySync.Unregister(this);
            m_ManagedParty = null;
        }

        /// <summary>
        ///     Handler to apply a received move command for this party.
        /// </summary>
        /// <exception cref="Exception"></exception>
        private void UpdateLocalMovement()
        {
            if (m_ManagedParty == null)
            {
                m_ManagedParty = m_Environment.GetMobilePartyById(State.PartyId);
                if (m_ManagedParty == null)
                {
                    Logger.Warn("Mobile party id {PartyId} not found", State.PartyId);
                    return;
                }
            }
            MovementData data = State.Movement.ToData();
            m_Environment.SetAuthoritative(m_ManagedParty, data);
            Replay.ReplayRecording?.Invoke(Id, m_ManagedParty, data);
        }

        private void UpdateLocalPosition()
        {
            if (m_ManagedParty == null)
            {
                m_ManagedParty = m_Environment.GetMobilePartyById(State.PartyId);
                if (m_ManagedParty == null)
                {
                    Logger.Warn("Mobile party id {PartyId} not found", State.PartyId);
                    return;
                }
            }

            m_ManagedParty.Position2D = State.MapPosition;
        }

        /// <summary>
        ///     Handler to be called when the control of this party changes to or from any player.
        /// </summary>
        private void OnPlayerControlledChanged()
        {
            MobileParty party = m_Environment.GetMobilePartyById(State.PartyId);
            m_Environment.SetIsPlayerControlled(party.Id, State.IsPlayerControlled);
        }
        
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        [NotNull] private readonly IEnvironmentClient m_Environment;
        [CanBeNull] private MobileParty m_ManagedParty;
    }
}
