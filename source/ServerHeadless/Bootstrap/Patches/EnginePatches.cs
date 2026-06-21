using HarmonyLib;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace ServerHeadless.Bootstrap.Patches
{
    /// <summary>
    /// Native engine calls that are reached through the Coop load path / object deserialization but
    /// have no meaning headless.
    /// </summary>
    [HarmonyPatch]
    internal class EnginePatches
    {
        // GameStateInterface.StartGame calls MouseManager.ShowCursor (native) after loading.
        [HarmonyPatch(typeof(MouseManager), nameof(MouseManager.ShowCursor))]
        [HarmonyPrefix]
        static bool ShowCursorPrefix() => false;

        // Item deserialization (HandArmor) registers a GPU morph mesh via the native engine.
        [HarmonyPatch(typeof(Utilities), nameof(Utilities.RegisterMeshForGPUMorph))]
        [HarmonyPrefix]
        static bool RegisterMeshForGPUMorphPrefix() => false;

        // Monster deserialization looks up skeleton bone indices from the native action set.
        [HarmonyPatch(typeof(MBActionSet), nameof(MBActionSet.GetBoneIndexWithId))]
        [HarmonyPrefix]
        static bool GetBoneIndexWithIdPrefix(ref sbyte __result)
        {
            __result = 0;
            return false;
        }

        // The game tells the engine which scene to render error reports against (the campaign map
        // screen does this every frame; SandBoxGameManager.OnGameEnd clears it). The call goes
        // straight into the native engine with no managed guard, so headless — where that engine was
        // never initialised — it dereferences a dead subsystem and takes the whole process down.
        // There is no error-report scene to set headless; skip it. The error that drove the game here
        // is surfaced instead by the HeadlessDebugManager installed in HeadlessBootstrap.
        [HarmonyPatch(typeof(MBDebug), nameof(MBDebug.SetErrorReportScene))]
        [HarmonyPrefix]
        static bool SetErrorReportScenePrefix() => false;
    }
}
