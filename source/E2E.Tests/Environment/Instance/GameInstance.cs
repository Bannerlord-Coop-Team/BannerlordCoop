using Common.Util;
using HarmonyLib;
using SandBox;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.ModuleManager;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace E2E.Tests.Environment.Instance;
public class GameInstance
{
    public MBObjectManager MBObjectManager { get; }

    public Module Module { get; }

    public Campaign Campaign { get; }

    public SandBoxGameManager GameManager { get; }

    public Game Game { get; }

    public static object @lock = new object();

    public GameInstance()
    {
        lock (@lock)
        {
            using(new AllowedThread())
            {
                Module = (Module)AccessTools.Constructor(typeof(Module)).Invoke(null);
                var modules = ModuleHelper.GetOfficialModuleIds().Append("Coop");
                ModuleHelper.InitializeModules(modules.ToArray());
                GameManager = new SandBoxGameManager(() => new Campaign(CampaignGameMode.Campaign));
                Campaign = new Campaign(CampaignGameMode.Campaign);
                Game = Game.CreateGame(Campaign, GameManager);
                MBObjectManager = MBObjectManager.Instance;

                Campaign.SiegeEventManager = new SiegeEventManager();
                Campaign.MapEventManager = new MapEventManager();
                Campaign.MapMarkerManager = new MapMarkerManager();

                RegisterType<ItemObject>(MBObjectManager);
                RegisterType<Settlement>(MBObjectManager);
                RegisterType<Hero>(MBObjectManager);
                RegisterType<MobileParty>(MBObjectManager);
                RegisterType<ItemObject>(MBObjectManager);
                RegisterType<TraitObject>(MBObjectManager);
                RegisterType<SkillObject>(MBObjectManager);
                RegisterType<PerkObject>(MBObjectManager);
                RegisterType<BannerEffect>(MBObjectManager);
                RegisterType<CharacterAttribute>(MBObjectManager);
                RegisterType<Clan>(MBObjectManager);


                SetStatics();

                Game.Initialize();

                Campaign._mapSceneWrapper = new MapScene();
            }
        }

    }


    private uint itemCounter = 0;
    private void RegisterType<T>(MBObjectManager mBObjectManager) where T : MBObjectBase
    {
        mBObjectManager.RegisterType<T>($"{typeof(T).Name}", $"{typeof(T).Name}s", itemCounter++, true, false);
    }

    public void SetStatics()
    {
        MBObjectManager.Instance = MBObjectManager;
        Campaign.Current = Campaign;
        Game.Current = Game;
        Module.CurrentModule = Module;
    }
}
