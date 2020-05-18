using NLog;
using RailgunNet;
using RailgunNet.Logic;
using RailgunNet.System.Types;
using RailgunNet.Util;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Persistence.World
{
    public class EventTimeControl : RailEvent
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public CampaignTimeControlMode RequestedTimeControlMode
        {
            get => (CampaignTimeControlMode) m_RequestedTimeControlMode;
            set => m_RequestedTimeControlMode = (byte) value;
        }

        [OnlyIn(Component.Server)]
        protected override void Execute(RailRoom room, RailController sender)
        {
            if (TryFind(EntityId, out WorldEntityServer entity))
            {
                Logger.Trace(
                    "Time control change request from {sender} to {request}.",
                    sender,
                    RequestedTimeControlMode);
                entity.State.TimeControlMode = RequestedTimeControlMode;
            }
            else
            {
                Logger.Warn("World entity {id} not found.", EntityId);
            }
        }

        #region synced data
        [EventData] public EntityId EntityId { get; set; }
        [EventData] private byte m_RequestedTimeControlMode { get; set; }
        #endregion
    }
}
