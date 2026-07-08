using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace ServerHeadless.Bootstrap.Patches
{
    /// <summary>
    /// No-ops the genuinely native-only setup methods that fire during game creation / load.
    ///
    /// Unlike the Coop test harness (which runs on synthetic objects and disables the whole data
    /// pipeline), we deliberately let the MANAGED data loading run — MBObjectManager.LoadXML,
    /// Game.SetBasicModels, ModuleHelper.GetModuleFullPath, Game.InitializeDefaultGameObjects — so the
    /// real cultures / items / skills / settlement data are present and the campaign's AfterLoad
    /// logic has the data it expects. Only the engine-native calls (graphics/audio/physics/module
    /// resource load) are stubbed here.
    /// </summary>
    [HarmonyPatch]
    internal class GamePatches
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            return new MethodBase[]
            {
                AccessTools.Method(typeof(Game), "InitializeParameters"),
                AccessTools.Method(typeof(NativeConfig), "OnConfigChanged"),
                AccessTools.Method(typeof(ConversationAnimationManager), "LoadConversationAnimData"),
                // Native engine module-resource load (MBAPI.IMBGame.LoadModuleData); the managed
                // object XML is loaded separately by Campaign.OnInitialize.
                AccessTools.Method(typeof(MBGameManager), "LoadModuleData"),
                // Indexes skeleton bones via native Skeleton.GetBoneIndexFromName (rendering/missions
                // only); not needed for the campaign-map simulation.
                AccessTools.Method(typeof(MBGameManager), "OnGameInitializationFinished"),
                // New-game loading step 2 calls the parameterless overload, a raw
                // MBAPI.IMBGame.StartNew() native call (null headless). The static
                // StartNewGame(MBGameManager) overload that pushes GameLoadingState must NOT be
                // patched — the load/new-game flows are driven through it.
                AccessTools.Method(typeof(MBGameManager), "StartNewGame", System.Type.EmptyTypes),
            };
        }

        static bool Prefix() => false;
    }
}
