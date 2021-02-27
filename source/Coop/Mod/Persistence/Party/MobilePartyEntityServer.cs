using System;
using JetBrains.Annotations;
using NLog;
using RailgunNet.Logic;
using RailgunNet.System.Types;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Persistence.Party
{
    /// <summary>
    ///     Railgun: Mobile party implementation for the server. One instance for each mobile party
    ///     that is registered in the Railgun room.
    /// </summary>
    public class MobilePartyEntityServer : RailEntityServer<MobilePartyState>, IMovementHandler
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        [NotNull] private readonly IEnvironmentServer m_Environment;
        [CanBeNull] private MobileParty m_Instance;

        public MobilePartyEntityServer([NotNull] IEnvironmentServer environment)
        {
            m_Environment = environment;
        }

        /// <summary>
        ///     Called when the controller of this party changes.
        /// </summary>
        protected override void OnControllerChanged()
        {
            if (Controller == null)
            {
                RegisterAsDefaultController();
            }
            else
            {
                UnregisterAsController();
                State.IsPlayerControlled = true;
            }
        }

        /// <summary>
        ///     Called when this party is added to the Railgun room.
        /// </summary>
        protected override void OnAdded()
        {
            RegisterAsDefaultController();
            MobileParty party = m_Environment.GetMobilePartyById(State.PartyId);
            if (party != null)
            {
                // Initialize state
                RequestMovement(party.GetMovementData()); 
            }
        }

        /// <summary>
        ///     Called when this party is removed from the Railgun room.
        /// </summary>
        protected override void OnRemoved()
        {
            UnregisterAsController();
        }

        /// <summary>
        ///     Registers handlers to intercept issued movement commands to this party and apply them
        ///     the authoritative state.
        /// </summary>
        /// <exception cref="Exception"></exception>
        private void RegisterAsDefaultController()
        {
            if (IsRemoving)
            {
                return;
            }
            
            if (m_Instance == null && Controller == null)
            {
                m_Instance = m_Environment.GetMobilePartyById(State.PartyId);
                State.IsPlayerControlled = false;
                if (m_Instance == null)
                {
                    Logger.Warn(
                        "Mobile party id {} not found in the local game state. Desync?",
                        State.PartyId);
                    return;
                }
                m_Environment.PartySync.RegisterLocalHandler(m_Instance, this);
            }
        }

        /// <summary>
        ///     Unregisters all handlers.
        /// </summary>
        private void UnregisterAsController()
        {
            m_Environment.PartySync.Unregister(this);
            m_Instance = null;
        }

        public Tick Tick => Room?.Tick ?? Tick.INVALID;

        /// <summary>
        ///     Handler to apply a movement command to the authoritative state.
        /// </summary>
        /// <param name="args">MovementData</param>
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

        public override string ToString()
        {
            return $"Party {State.PartyId} ({Id}): {m_Instance}";
        }
    }
}
