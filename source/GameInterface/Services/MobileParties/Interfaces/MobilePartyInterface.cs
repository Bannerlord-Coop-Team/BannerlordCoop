using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.MobileParties.Interfaces
{
    internal interface IMobilePartyInterface : IGameAbstraction
    {
        void ManageNewParty(MobileParty party);
    }

    internal class MobilePartyInterface : IMobilePartyInterface
    {
        private static readonly MethodInfo PartyBase_OnFinishLoadState = typeof(PartyBase).GetMethod("OnFinishLoadState", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo AddMobileParty = typeof(CampaignObjectManager).GetMethod("AddMobileParty", BindingFlags.Instance | BindingFlags.NonPublic);
        public void ManageNewParty(MobileParty party)
        {
            AddMobileParty.Invoke(Campaign.Current.CampaignObjectManager, new object[] { party });

            party.IsVisible = true;

            PartyBase_OnFinishLoadState.Invoke(party.Party, null);
        }
    }
}
