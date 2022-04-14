using Coop.Mod.GameSync.Party;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.GameSync
{
    public class Initializer
    {
        public static void SetupSyncAfterLoad()
        {
            foreach (MobileParty party in MobileParty.All)
            {
                MobilePartyManaged.MakeManaged(party, false);
            }
        }
    }
}
