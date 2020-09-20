using System.ComponentModel;
using RailgunNet.Logic;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Persistence.World
{
    public class WorldState : RailState, INotifyPropertyChanged
    {
#pragma warning disable 67
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore 67
        public override string ToString()
        {
            return $"{CampaignTimeTicks} :: {TimeControl} {TimeControlLock}";
        }

        #region synced data
        [Mutable] public CampaignTimeControlMode TimeControl { get; set; }

        [Mutable] public bool TimeControlLock { get; set; }

        [Mutable] public long CampaignTimeTicks { get; set; }
        #endregion
    }
}
