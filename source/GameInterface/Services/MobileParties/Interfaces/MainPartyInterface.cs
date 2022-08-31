using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Interfaces
{
    internal class MainPartyInterface : IMainPartyInterface
    {
        public void RemoveMainParty()
        {
            MobileParty.MainParty?.RemoveParty();
        }
    }
}
