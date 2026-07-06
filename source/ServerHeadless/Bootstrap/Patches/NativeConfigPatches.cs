using HarmonyLib;
using TaleWorlds.Engine;

namespace ServerHeadless.Bootstrap.Patches
{
    /// <summary>
    /// <see cref="NativeConfig"/> reads engine configuration from the native side, which never
    /// initialises headless. <see cref="NativeConfig.CheatMode"/> gates the game's console cheats
    /// (<c>CampaignCheats.CheckCheatUsage</c> → <c>Game.CheatMode</c> →
    /// <c>MBGameManager.CheatMode</c> → here). The headless operator console (see
    /// <c>HeadlessConsole</c>) is the server's admin interface, so cheats are always allowed.
    /// </summary>
    [HarmonyPatch(typeof(NativeConfig), nameof(NativeConfig.CheatMode), MethodType.Getter)]
    internal class NativeConfigPatches
    {
        static bool Prefix(ref bool __result)
        {
            __result = true;
            return false;
        }
    }
}
