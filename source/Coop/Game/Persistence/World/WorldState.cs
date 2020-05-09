using System.ComponentModel;
using RailgunNet.Logic;
using TaleWorlds.CampaignSystem;

namespace Coop.Game.Persistence.World
{
    public class WorldState : RailState, INotifyPropertyChanged
    {
        public CampaignTimeControlMode TimeControlMode
        {
            get => (CampaignTimeControlMode) m_TimeControlMode;
            set => m_TimeControlMode = (byte) value;
        }

        #region synced data
        [Mutable] private byte m_TimeControlMode { get; set; }
        #endregion

#pragma warning disable 67
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore 67
    }
}
