using HarmonyLib;
using System;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ModuleManager;
using TaleWorlds.ObjectSystem;
using TaleWorlds.SaveSystem;
using Module = TaleWorlds.MountAndBlade.Module;

namespace ServerHeadless.Bootstrap
{
    /// <summary>
    /// Stands the game up enough to load a save and tick a campaign WITHOUT the native engine.
    ///
    /// The native host (Bannerlord.exe → WotsMainSDLL) cannot be bootstrapped from a plain managed
    /// process (see README), so instead of <c>Module.Initialize()</c> we mirror the Coop test
    /// harness (<c>GameInterface.Tests.Bootstrap.GameBootStrap</c>): Harmony-patch the native-only
    /// methods to no-ops, initialise the object manager, and construct the module directly via its
    /// (publicized) constructor. Unlike the test harness we do NOT create a fresh campaign here —
    /// the campaign comes from loading the selected save.
    /// </summary>
    internal static class HeadlessBootstrap
    {
        private const string HarmonyId = "ServerHeadless";

        private static bool _initialized;

        public static void Initialize(string applicationRoot)
        {
            if (_initialized) return;
            _initialized = true;

            // The native engine normally installs the platform file helper; provide a managed one so
            // the save system can map virtual paths (User -> Documents\...) to real files.
            Common.PlatformFileHelper = new HeadlessPlatformFileHelper(applicationRoot);

            // Apply every [HarmonyPatch] in this assembly (Bootstrap/Patches/*) — these neutralise
            // the native-only calls that would otherwise crash a headless process.
            new Harmony(HarmonyId).PatchAll(typeof(HeadlessBootstrap).Assembly);

            InitializeObjectManager();
            InitializeModule();

            // Banner colour palette is normally filled from XML (which we skip); an empty palette is
            // enough for clans' AfterLoad banner-colour fixups not to NRE.
            BannerManager.Initialize();

            // Populate the active module list (ModuleHelper.GetModules), read from each module's
            // SubModule.xml under the game root. Module.OnBeforeGameStart and the save loader query it.
            ModuleHelper.InitializeModules(RequiredModules);

            // Register each module's <Xmls> declarations so MBObjectManager.LoadXML / LoadGameTexts
            // can find the data files (cultures, items, skills, strings, …). Normally done by the
            // native module-data load; here we drive the managed reader directly.
            foreach (string moduleId in RequiredModules)
            {
                XmlResource.GetXmlListAndApply(moduleId);
            }

            // The file save driver lets MBSaveLoad enumerate and read the on-disk .sav files.
            MBSaveLoad.SetSaveDriver(new AsyncFileSaveDriver());
        }

        /// <summary>Submodules to make active, in dependency order.</summary>
        private static readonly string[] RequiredModules =
        {
            "Native",
            "SandBoxCore",
            "SandBox",
            "StoryMode",
            "Coop",
        };

        private static void InitializeObjectManager()
        {
            if (MBObjectManager.Instance != null) return;

            // Just create the manager. Do NOT pre-register object types here: the game registers them
            // itself during load with the correct XML element names (e.g. ItemObject -> "Item"/"Items",
            // Monster -> "Monster"/"Monsters"), and RegisterType skips duplicates — pre-registering with
            // wrong names (typeof(T).Name) would block the real registration and break LoadXML for those
            // types (items/monsters wouldn't load, leaving e.g. horses as invalid items).
            MBObjectManager.Init();
        }

        private static void InitializeModule()
        {
            if (Module.CurrentModule != null) return;

            // Construct Module via its private parameterless constructor and assign the private
            // CurrentModule setter. The constructor wires GlobalGameStateManager and sets
            // GameStateManager.Current; we deliberately bypass Module.CreateModule(), whose trailing
            // Utilities.SetLoadingScreenPercentage() call is native and NREs headless.
            // (Reflection rather than the publicizer: Krafs.Publicizer 2.3.0 exposes internal members
            // of TaleWorlds.MountAndBlade but not these private ones.)
            var module = (Module)Activator.CreateInstance(typeof(Module), nonPublic: true);
            typeof(Module)
                .GetProperty(nameof(Module.CurrentModule), BindingFlags.Public | BindingFlags.Static)
                .GetSetMethod(nonPublic: true)
                .Invoke(null, new object[] { module });
        }
    }
}
