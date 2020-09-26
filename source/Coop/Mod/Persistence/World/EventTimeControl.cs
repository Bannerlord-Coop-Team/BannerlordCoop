using System;
using NLog;
using RailgunNet;
using RailgunNet.Logic;
using RailgunNet.System.Types;
using RailgunNet.Util;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Persistence.World
{
    /// <summary>
    ///     Event sent by clients to request a change to the campaigns time control mode or lock.
    /// </summary>
    public class EventTimeControl : RailEvent
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public ValueTuple<CampaignTimeControlMode, bool> RequestedTimeControlMode
        {
            get =>
                ((CampaignTimeControlMode) m_RequestedTimeControlMode,
                    m_RequestedTimeControlModeLock == 1);
            set =>
                (m_RequestedTimeControlMode, m_RequestedTimeControlModeLock) = ((byte) value.Item1,
                    value.Item2 ? (byte) 1 : (byte) 0);
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
                (entity.RequestedTimeControlMode, entity.RequestedTimeControlModeLock) =
                    RequestedTimeControlMode;
            }
            else
            {
                Logger.Warn("World entity {id} not found.", EntityId);
            }
        }

        #region synced data
        [EventData] public EntityId EntityId { get; set; }
        [EventData] private byte m_RequestedTimeControlMode { get; set; }
        [EventData] private byte m_RequestedTimeControlModeLock { get; set; }
        #endregion
    }
}
