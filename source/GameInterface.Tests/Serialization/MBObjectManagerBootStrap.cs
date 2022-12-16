using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace GameInterface.Tests.Serialization
{
    internal class GameBootStrap
    {

        private static object _lock = new object();

        private static bool _isInitialized = false;

        /// <summary>
        /// Initializes any game functionality used for testing
        /// Currently initializes the following:
        /// - MBObjectManager
        /// </summary>
        public static void Initialize()
        {
            lock(_lock)
            {
                if (_isInitialized) return;

                _isInitialized = true;

                InitializeMBObjectManager();
            }
        }

        private static void InitializeMBObjectManager()
        {
            if (MBObjectManager.Instance != null) return;

            MBObjectManager.Init();
            MBObjectManager.Instance.RegisterType<ItemObject>("Item", "Items", 4U, true, false);
            MBObjectManager.Instance.RegisterType<Settlement>("Settlement", "Settlements", 4U, true, false);
            MBObjectManager.Instance.RegisterType<Hero>("Hero", "Heros", 5U, true, false);
            MBObjectManager.Instance.RegisterType<MobileParty>("MobileParty", "MobilePartys", 5U, true, false);
            MBObjectManager.Instance.RegisterType<ItemObject>("ItemObject", "ItemObjects", 4U, true, false);
            MBObjectManager.Instance.RegisterType<TraitObject>("TraitObject", "TraitObjects", 4U, true, false);
            MBObjectManager.Instance.RegisterType<SkillObject>("SkillObject", "SkillObjects", 4U, true, false);
            MBObjectManager.Instance.RegisterType<PerkObject>("PerkObject", "PerkObjects", 4U, true, false);
            MBObjectManager.Instance.RegisterType<BannerEffect>("BannerEffect", "BannerEffects", 4U, true, false);
        }
    }
}
