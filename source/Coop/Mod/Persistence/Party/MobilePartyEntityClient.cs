using System;
using JetBrains.Annotations;
using NLog;
using RailgunNet.Logic;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Persistence.Party
{
    public class MobilePartyEntityClient : RailEntityClient<MobilePartyState>
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        [NotNull] private readonly IEnvironmentClient m_Environment;
        [CanBeNull] private MobileParty m_Instance;

        public MobilePartyEntityClient([NotNull] IEnvironmentClient environment)
        {
            m_Environment = environment;
        }

        private void GoToPosition(object val)
        {
            MovementData data = val as MovementData;
            if (data == null)
            {
                throw new ArgumentException(nameof(data));
            }

            Logger.Trace("[{tick}] Request move entity {id} to '{position}'.", Room.Tick, Id, data);
            Room.RaiseEvent<EventPartyMoveTo>(
                e =>
                {
                    e.EntityId = Id;
                    e.Movement = new MovementState
                    {
                        DefaultBehavior = data.DefaultBehaviour,
                        Position = data.TargetPosition,
                        TargetPartyIndex =
                            data.TargetParty?.Party.Index ?? MovementState.InvalidPartyIndex
                    };
                });
        }

        private void UpdateLocalMovement()
        {
            MobileParty party = m_Environment.GetMobilePartyByIndex(State.PartyId);
            if (party == null) return;
            Logger.Trace(
                "[{tick}] Received move entity {id} ('{party}') to '{position}'.",
                Room.Tick,
                Id,
                party,
                State.Movement);
            m_Environment.TargetPosition.SetTyped(
                party,
                new MovementData
                {
                    DefaultBehaviour = State.Movement.DefaultBehavior,
                    TargetPosition = State.Movement.Position,
                    TargetParty =
                        State.Movement.TargetPartyIndex !=
                        MovementState.InvalidPartyIndex ?
                            m_Environment.GetMobilePartyByIndex(
                                State.Movement.TargetPartyIndex) :
                            null
                });
        }

        protected override void OnControllerChanged()
        {
            if (Controller != null)
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
            if (m_Instance == null && Controller != null)
            {
                m_Instance = m_Environment.GetMobilePartyByIndex(State.PartyId);
                if (m_Instance == null)
                {
                    throw new Exception($"Mobile party id {State.PartyId} not found.");
                }

                m_Environment.TargetPosition.SetSyncHandler(m_Instance, GoToPosition);
                State.OnMovementChanged += UpdateLocalMovement;
            }
        }

        private void Unregister()
        {
            if (m_Instance != null)
            {
                m_Environment.TargetPosition.RemoveSyncHandler(m_Instance);
                State.OnMovementChanged -= UpdateLocalMovement;
            }
        }
    }
}
