using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Patch.GameStates
{
    internal class MainMenuPatch
    {
        private static bool MainMenuReady { get; set; } = false;

        [HarmonyPatch(typeof(MBInitialScreenBase), "OnFrameTick")]
        class MainMenuReadyPatch
        {
            static void Postfix(ref object __instance)
            {
                if (!MainMenuReady && MBMusicManager.Current != null)
                {
                    MainMenuReady = true;
                    //MessageBroker.Instance.Publish(__instance, new MainMenuEntered());
                }
            }
        }
    }
}
