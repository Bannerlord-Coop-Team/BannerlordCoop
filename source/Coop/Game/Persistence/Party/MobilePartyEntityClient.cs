using System;
using JetBrains.Annotations;
using NLog;
using RailgunNet.Logic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using Logger = NLog.Logger;

namespace Coop.Game.Persistence.Party
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

            Logger.Trace(
                "[{tick}] Request move entity {id} ('{party}') to '{position}'.",
                Room.Tick,
                Id,
                m_Instance,
                data);
            Room.RaiseEvent<EventPartyMoveTo>(
                e =>
                {
                    e.EntityId = Id;
                    e.Movement = new MovementState()
                    {
                        DefaultBehavior = data.DefaultBehaviour,
                        Position = data.TargetPosition
                    };
                });
        }

        private void UpdateLocalMovement()
        {
            Logger.Trace(
                "[{tick}] Received move entity {id} ('{party}') to '{position}'.",
                Room.Tick,
                Id,
                m_Instance,
                State.Movement);
            m_Environment.TargetPosition.SetTyped(m_Instance, new MovementData()
            {
                DefaultBehaviour = State.Movement.DefaultBehavior,
                TargetPosition = State.Movement.Position
            });
        }

        protected override void OnAdded()
        {
            m_Instance = m_Environment.GetMobilePartyByIndex(State.PartyId);

            m_Environment.TargetPosition.SyncHandler += GoToPosition;
            State.OnMovementChanged += UpdateLocalMovement;
        }

        protected override void OnRemoved()
        {
            m_Environment.TargetPosition.SyncHandler -= GoToPosition;
            State.OnMovementChanged -= UpdateLocalMovement;
            m_Instance = null;
        }
    }
}
