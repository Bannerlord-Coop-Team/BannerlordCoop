using HarmonyLib;
using SandBox;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using Xunit;

namespace GameInterface.Tests.Bootstrap
{
    internal class GameBootStrap
    {
        private static object @lock = new object();

        private const string HarmonyID = "Coop.Testing";
        private static Harmony harmony;

        private static Campaign current;

        /// <summary>
        /// Initializes any game functionality used for testing
        /// Currently initializes the following:
        /// - MBObjectManager
        /// </summary>
        public static void Initialize()
        {
            lock (@lock)
            {
                if (Harmony.HasAnyPatches(HarmonyID) == false)
                {
                    harmony = new Harmony(HarmonyID);
                    harmony.PatchAll();

                    // MBObjectManager is the default object managment system used by Bannerlord
                    InitializeMBObjectManager();
                    // Campaign stores most of the data for the campaign map
                    InitializeCampaign();
                    // Game provides saving and loading functionality, as well as some default values used in the game
                    InitializeGame();
                }

                Assert.NotNull(Campaign.Current);
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

        private static readonly PropertyInfo Campaign_Current = typeof(Campaign).GetProperty(nameof(Campaign.Current))!;
        private static readonly PropertyInfo Campaign_CampaignEvents = typeof(Campaign).GetProperty("CampaignEvents", BindingFlags.NonPublic | BindingFlags.Instance)!;
        private static readonly PropertyInfo Campaign_CustomPeriodicCampaignEvents = typeof(Campaign).GetProperty("CustomPeriodicCampaignEvents", BindingFlags.NonPublic | BindingFlags.Instance)!;
        private static readonly PropertyInfo Campaign_CampaignEventDispatcher = typeof(Campaign).GetProperty("CampaignEventDispatcher", BindingFlags.NonPublic | BindingFlags.Instance)!;
        private static Action<Campaign> Campaign_OnInitialize = typeof(Campaign).GetMethod("OnInitialize", BindingFlags.NonPublic | BindingFlags.Instance)!.CreateDelegate<Action<Campaign>>();
        private static readonly MethodInfo InitializeManagerObjectLists =
            typeof(CampaignObjectManager).GetMethod("InitializeManagerObjectLists", BindingFlags.NonPublic | BindingFlags.Instance)!;

        private static void InitializeCampaign()
        {
            Debug.DebugManager = new TestDebugger();

            current = new Campaign(CampaignGameMode.Campaign);
            Campaign_Current.SetValue(null, current);
            Campaign_CampaignEvents.SetValue(current, new CampaignEvents());
            Campaign_CustomPeriodicCampaignEvents.SetValue(current, new List<MBCampaignEvent>());

            var ctor = typeof(CampaignEventDispatcher).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, new[] { typeof(IEnumerable<CampaignEventReceiver>) })!;
            CampaignEventDispatcher eventDispatcher = (CampaignEventDispatcher)ctor.Invoke(new object[] { Array.Empty<CampaignEventReceiver>() })!;
            Campaign_CampaignEventDispatcher.SetValue(current, eventDispatcher);
            InitializeManagerObjectLists.Invoke(current.CampaignObjectManager, null);
        }

        private static readonly FieldInfo Game_Current = typeof(Game).GetField("_current", BindingFlags.NonPublic | BindingFlags.Static)!;
        private static void InitializeGame()
        {
            SandBoxGameManager gameManager = (SandBoxGameManager)FormatterServices.GetUninitializedObject(typeof(SandBoxGameManager));

            Game game = Game.CreateGame(Campaign.Current, gameManager);
            Game_Current.SetValue(null, game);
        }
    }
}
