using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;

namespace Coop.Game
{
    public class CoopEvents
    {
        public readonly MbEvent<MobileParty> OnBeforePlayerPartySpawned = new MbEvent<MobileParty>();
        public void RemoveListeners(object obj)
        {
            OnBeforePlayerPartySpawned.ClearListeners(obj);
        }
    }
}
