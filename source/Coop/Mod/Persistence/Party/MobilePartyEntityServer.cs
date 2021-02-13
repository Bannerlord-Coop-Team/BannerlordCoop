using System;
using JetBrains.Annotations;
using NLog;
using RailgunNet.Logic;
using RailgunNet.System.Types;
using RemoteAction;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

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
                Register();
            }
            else
            {
                Unregister();
            }
        }

        /// <summary>
        ///     Called when this party is added to the Railgun room.
        /// </summary>
        protected override void OnAdded()
        {
            Register();
        }

        /// <summary>
        ///     Called when this party is removed from the Railgun room.
        /// </summary>
        protected override void OnRemoved()
        {
            Unregister();
        }

        /// <summary>
        ///     Registers handlers to intercept issued movement commands to this party and apply them
        ///     the authoritative state.
        /// </summary>
        /// <exception cref="Exception"></exception>
        private void Register()
        {
            if (IsRemoving)
            {
                return;
            }
            
            if (m_Instance == null && Controller == null)
            {
                m_Instance = m_Environment.GetMobilePartyByIndex(State.PartyId);
                State.IsPlayerControlled = false;
                if (m_Instance == null)
                {
                    Logger.Error(
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
        private void Unregister()
        {
            if (m_Instance != null)
            {
                State.IsPlayerControlled = true;
            }
        }

        public Tick Tick => Room.Tick;

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
            State.Movement.Position = data.TargetPosition;
            State.Movement.TargetPartyIndex = data.TargetParty?.Id ?? MovementState.InvalidIndex;
            State.Movement.SettlementIndex =
                data.TargetSettlement?.Id ?? MovementState.InvalidIndex;
        }

        public MovementData GetLatest()
        {
            return new MovementData
            {
                DefaultBehaviour = State.Movement.DefaultBehavior,
                TargetPosition = State.Movement.Position,
                TargetParty = State.Movement.TargetPartyIndex != MovementState.InvalidIndex
                    ? MBObjectManager.Instance.GetObject(State.Movement.TargetPartyIndex) as
                        MobileParty
                    : null,
                TargetSettlement = State.Movement.SettlementIndex != MovementState.InvalidIndex
                    ? MBObjectManager.Instance.GetObject(
                        State.Movement.SettlementIndex) as Settlement
                    : null
            };
        }

        public override string ToString()
        {
            return $"Party {State.PartyId} ({Id}): {m_Instance}";
        }
    }
}
