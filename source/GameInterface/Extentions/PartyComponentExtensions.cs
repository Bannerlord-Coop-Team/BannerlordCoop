using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Extentions
{
    internal static class PartyComponentExtensions
    {
        private static readonly PropertyInfo PartyComponent_MobileParty = typeof(PartyComponent).GetProperty(nameof(PartyComponent.MobileParty));
        public static void SetMobileParty(this PartyComponent component, MobileParty party)
        {
            PartyComponent_MobileParty.SetValue(component, party);
        }
    }
}
