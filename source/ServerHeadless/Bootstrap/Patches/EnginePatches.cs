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
        // DebugGameInterface.StartGame calls MouseManager.ShowCursor (native) after loading.
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
    }
}
