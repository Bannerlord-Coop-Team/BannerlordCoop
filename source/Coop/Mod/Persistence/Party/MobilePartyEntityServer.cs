using System;
using JetBrains.Annotations;
using NLog;
using RailgunNet.Logic;
using RemoteAction;
using Sync.Behaviour;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Persistence.Party
{
    /// <summary>
    ///     Railgun: Mobile party implementation for the server. One instance for each mobile party
    ///     that is registered in the Railgun room.
    /// </summary>
    public class MobilePartyEntityServer : RailEntityServer<MobilePartyState>
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

        /// <summary>
        ///     Handler to apply a movement command to the authoritative state.
        /// </summary>
        /// <param name="args">MovementData</param>
        /// <exception cref="ArgumentException"></exception>
        private ECallPropagation SetMovement(object[] args)
        {
            MovementData data = args.Length > 0 ? args[0] as MovementData : null;
            if (data == null)
            {
                throw new ArgumentException(nameof(data));
            }

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
            return ECallPropagation.CallOriginal;
        }

        public override string ToString()
        {
            return $"Party {State.PartyId} ({Id}): {m_Instance}";
        }
    }
}
