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

        private void GoToPosition(object data)
        {
            if (!(data is Vec2))
            {
                throw new ArgumentException(nameof(data));
            }

            Logger.Trace(
                "[T {tick}] Request move entity {id} ('{party}') to '{position}'.",
                Room.Tick,
                Id,
                m_Instance,
                (Vec2) data);
            Room.RaiseEvent<EventPartyMoveTo>(
                e =>
                {
                    e.EntityId = Id;
                    e.Position = (Vec2) data;
                });
        }

        private void UpdateLocalPosition()
        {
            Logger.Trace(
                "[T {tick}] Received move entity {id} ('{party}') to '{position}' on {authTick}.",
                Room.Tick,
                Id,
                m_Instance,
                State.Position,
                AuthTick);
            m_Environment.TargetPosition.Set(m_Instance, State.Position);
        }

        protected override void OnAdded()
        {
            m_Instance = m_Environment.GetMobilePartyByIndex(State.PartyId);

            m_Environment.TargetPosition.SyncHandler += GoToPosition;
            State.OnPositionChanged += UpdateLocalPosition;
        }

        protected override void OnRemoved()
        {
            m_Environment.TargetPosition.SyncHandler -= GoToPosition;
            State.OnPositionChanged -= UpdateLocalPosition;
            m_Instance = null;
        }
    }
}
