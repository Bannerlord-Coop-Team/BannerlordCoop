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
            RegisterType<ItemObject>();
            RegisterType<Settlement>();
            RegisterType<Hero>();
            RegisterType<MobileParty>();
            RegisterType<ItemObject>();
            RegisterType<TraitObject>();
            RegisterType<SkillObject>();
            RegisterType<PerkObject>();
            RegisterType<BannerEffect>();
            RegisterType<CharacterAttribute>();
        }

        private static uint itemCounter = 0;
        private static void RegisterType<T>() where T : MBObjectBase
        {
            MBObjectManager.Instance.RegisterType<T>($"{typeof(T).Name}", $"{typeof(T).Name}s", itemCounter++, true, false);
        }
    }
}
