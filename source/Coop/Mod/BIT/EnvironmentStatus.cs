using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.SandBox.CampaignBehaviors;

namespace Coop.Mod.BIT
{
    class EnvironmentStatus
    {
        public bool HostOnline
        {
            get
            {
                return true;
            }
        }
        public int GameInstances { 
            get
            {
                return 1;
            } 
        }

        

        #region Private
        private bool IsHostOnline()
        {
            throw new NotImplementedException();
        }

        private int GetConnectedGameCount()
        {
            throw new NotImplementedException();
        }


        #endregion
    }


}
