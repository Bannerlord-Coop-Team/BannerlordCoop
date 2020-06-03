using System;
using JetBrains.Annotations;
using NLog;
using RailgunNet.Logic;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Persistence.Party
{
    public class MobilePartyEntityServer : RailEntityServer<MobilePartyState>
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        [NotNull] private readonly IEnvironmentServer m_Environment;
        [CanBeNull] private MobileParty m_Instance;

        public MobilePartyEntityServer([NotNull] IEnvironmentServer environment)
        {
            m_Environment = environment;
        }

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

        protected override void OnAdded()
        {
            Register();
        }

        protected override void OnRemoved()
        {
            Unregister();
        }

        private void Register()
        {
            if (m_Instance == null && Controller == null)
            {
                m_Instance = m_Environment.GetMobilePartyByIndex(State.PartyId);
                if (m_Instance == null)
                {
                    throw new Exception($"Mobile party id {State.PartyId} not found.");
                }

                m_Environment.TargetPosition.SetHandler(m_Instance, GoToPosition);
            }
        }

        private void Unregister()
        {
            if (m_Instance != null)
            {
                m_Environment.TargetPosition.RemoveHandler(m_Instance);
            }
        }

        private void GoToPosition(object val)
        {
            MovementData data = val as MovementData;
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
        }

        public override string ToString()
        {
            return $"Party {State.PartyId} ({Id}): {m_Instance}";
        }
    }
}
