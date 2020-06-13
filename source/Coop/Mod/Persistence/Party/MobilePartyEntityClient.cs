using System;
using JetBrains.Annotations;
using NLog;
using RailgunNet.Logic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

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
                        TargetPartyIndex = data.TargetParty?.Id ?? MovementState.InvalidIndex,
                        SettlementIndex = data.TargetSettlement?.Id ?? MovementState.InvalidIndex
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
                        MovementState.InvalidIndex ?
                            MBObjectManager.Instance.GetObject(
                                    State.Movement.TargetPartyIndex) as
                                MobileParty :
                            null,
                    TargetSettlement =
                        State.Movement.SettlementIndex != MovementState.InvalidIndex ?
                            MBObjectManager.Instance.GetObject(
                                    State.Movement.SettlementIndex) as
                                Settlement :
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
            State.OnMovementChanged += UpdateLocalMovement;
        }

        protected override void OnRemoved()
        {
            State.OnMovementChanged -= UpdateLocalMovement;
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

                m_Environment.TargetPosition.SetHandler(m_Instance, GoToPosition);
            }
        }

        private void Unregister()
        {
            if (m_Instance != null)
            {
                m_Environment.TargetPosition.RemoveHandler(m_Instance);
                m_Instance = null;
            }
        }

        public override string ToString()
        {
            return $"Party {State.PartyId} ({Id}): {m_Instance}";
        }
    }
}
