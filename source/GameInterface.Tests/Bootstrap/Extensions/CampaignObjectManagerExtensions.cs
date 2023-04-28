using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Tests.Bootstrap.Extensions
{
    internal static class CampaignObjectManagerExtensions
    {
        private static readonly MethodInfo CampaignObjectManager_AddMobileParty = 
            typeof(CampaignObjectManager)
            .GetMethod("AddMobileParty", 
                BindingFlags.NonPublic | 
                BindingFlags.Instance);

        private static Action<CampaignObjectManager, MobileParty> AddMobileParty_Delegate = 
            (Action<CampaignObjectManager, MobileParty>)Delegate.CreateDelegate(typeof(Action<CampaignObjectManager, MobileParty>), 
                CampaignObjectManager_AddMobileParty);

        public static void AddMobileParty(this CampaignObjectManager objectManager, MobileParty party)
        {
            AddMobileParty_Delegate(objectManager, party);
        }

        private static readonly MethodInfo CampaignObjectManager_AddHero =
            typeof(CampaignObjectManager)
            .GetMethod("AddHero",
                BindingFlags.NonPublic |
                BindingFlags.Instance);

        private static Action<CampaignObjectManager, Hero> AddHero_Delegate =
            (Action<CampaignObjectManager, Hero>)Delegate.CreateDelegate(typeof(Action<CampaignObjectManager, Hero>),
                CampaignObjectManager_AddHero);

        public static void AddHero(this CampaignObjectManager objectManager, Hero hero)
        {
            AddHero_Delegate(objectManager, hero);
        }
    }
}
