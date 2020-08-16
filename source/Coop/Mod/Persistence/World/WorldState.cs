using System;
using System.ComponentModel;
using RailgunNet.Logic;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Persistence.World
{
    public class WorldState : RailState, INotifyPropertyChanged
    {
        public ValueTuple<CampaignTimeControlMode, bool> TimeControlMode
        {
            get => ((CampaignTimeControlMode)m_TimeControlMode, m_TimeControlModeLock == 1);
            set => (m_TimeControlMode, m_TimeControlModeLock) = ((byte)value.Item1, value.Item2 ? (byte)1: (byte)0);
        }

        public long CampaignTimeTicks
        {
            get => m_CampaignTimeTicks;
            set => m_CampaignTimeTicks = value;
        }

        #region synced data
        [Mutable] private byte m_TimeControlMode { get; set; }
        [Mutable] private byte m_TimeControlModeLock { get; set; }
        [Mutable] private long m_CampaignTimeTicks { get; set; }
        #endregion

#pragma warning disable 67
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore 67
        public override string ToString()
        {
            return $"{CampaignTimeTicks} :: {TimeControlMode}";
        }
    }
}
