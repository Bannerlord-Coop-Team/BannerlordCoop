using HarmonyLib;
using SandBox;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ModuleManager;
using TaleWorlds.ObjectSystem;
using TaleWorlds.SaveSystem.Load;
using Xunit;

namespace GameInterface.Tests.Bootstrap
{
    public class GameBootStrap
    {
        private static object @lock = new object();

        private const string HarmonyID = "Coop.Testing";
        private static Harmony harmony;

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
                    // Game provides saving and loading functionality, as well as some default values used in the game
                    InitializeGame();
                    // Campaign stores most of the data for the campaign map
                    //InitializeCampaign();

                    Assert.NotNull(Campaign.Current);
                }
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

        private static void InitializeGame()
        {
            TaleWorlds.MountAndBlade.Module.CurrentModule = new TaleWorlds.MountAndBlade.Module();
            SandBoxGameManager gameManager = new SandBoxGameManager(new LoadResult());

            var modules = ModuleHelper.GetOfficialModuleIds().Append("Coop");

            ModuleHelper.InitializeModules(modules.ToArray());

            Campaign campaign = new Campaign(CampaignGameMode.Campaign);
            Campaign.Current = campaign;
            Game game = Game.CreateGame(campaign, gameManager);

            game.Initialize();

            campaign._mapSceneWrapper = new MapScene();
        }
    }
}
