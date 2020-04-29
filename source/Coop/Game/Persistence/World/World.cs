using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace Coop.Game.Persistence.World
{
    public class World
    {
        #region synced data
        public CampaignTimeControlMode TimeControlMode 
        {
            get => Environment.Current.TimeControlMode;
            set => Environment.Current.TimeControlMode = value;
        }
        #endregion

        public World()
        {
            Reset();
        }
        public void Reset()
        {
            TimeControlMode = CampaignTimeControlMode.Stop;
        }
    }
}
