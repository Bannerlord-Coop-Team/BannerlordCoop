using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.Library;
using SandBox;
using HarmonyLib;

namespace GameInterface.Tests.Bootstrap
{
    internal class GameBootStrap
    {

        private static object @lock = new object();

        private static bool isInitialized = false;

        private static Harmony harmony = new Harmony("Coop.Testing");

        /// <summary>
        /// Initializes any game functionality used for testing
        /// Currently initializes the following:
        /// - MBObjectManager
        /// </summary>
        public static void Initialize()
        {
            lock (@lock)
            {
                if (isInitialized) return;

                isInitialized = true;

                harmony.PatchAll();

                InitializeMBObjectManager();
                InitializeCampaign();
                InitializeGame();
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

        private static readonly PropertyInfo Campaign_Current = typeof(Campaign).GetProperty(nameof(Campaign.Current));
        private static readonly PropertyInfo Campaign_CampaignEvents = typeof(Campaign).GetProperty("CampaignEvents", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly PropertyInfo Campaign_CustomPeriodicCampaignEvents = typeof(Campaign).GetProperty("CustomPeriodicCampaignEvents", BindingFlags.NonPublic | BindingFlags.Instance);

        private static void InitializeCampaign()
        {
            if (Campaign.Current != null) return;

            Debug.DebugManager = new TestDebugger();

            Campaign campaign = new Campaign(CampaignGameMode.Campaign);
            Campaign_Current.SetValue(null, campaign);
            Campaign_CampaignEvents.SetValue(campaign, new CampaignEvents());
            Campaign_CustomPeriodicCampaignEvents.SetValue(campaign, new List<MBCampaignEvent>());
        }

        private static readonly FieldInfo Game_Current = typeof(Game).GetField("_current", BindingFlags.NonPublic | BindingFlags.Static);
        private static void InitializeGame()
        {
            SandBoxGameManager gameManager = (SandBoxGameManager)FormatterServices.GetUninitializedObject(typeof(SandBoxGameManager));

            Game game = Game.CreateGame(Campaign.Current, gameManager);
            Game_Current.SetValue(null, game);
        }
    }
}
