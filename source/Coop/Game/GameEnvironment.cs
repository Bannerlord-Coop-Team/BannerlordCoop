using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;

namespace Coop.Game
{
    class GameEnvironment : Persistence.IEnvironment
    {
        public CampaignTimeControlMode TimeControlMode
        {
            get => Campaign.Current.TimeControlMode;
            set => Patch.TimeControl.SetForced_Campaign_TimeControlMode(CampaignTimeControlMode.Stop);
        }
    }
}
