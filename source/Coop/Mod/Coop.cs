using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod
{
    public interface ICoop
    {
    }

    public class Coop : ICoop
    {
        ICoopNetwork _network;

        public Coop(ICoopNetwork network)
        {
            _network = network;
        }
    }

    
}
