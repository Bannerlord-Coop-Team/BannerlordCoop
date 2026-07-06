using System.Diagnostics;
using System.IO;
using System.Linq;
using TaleWorlds.ModuleManager;

namespace GameInterface.Services.GameState;

/// <summary>
/// Resolves how to spawn the managed dedicated-server process: the game engine executable
/// and the active module list, both read from the running game so the spawned server loads
/// the same engine and mods as the hosting client.
/// </summary>
public static class ManagedServerLauncher
{
    /// <summary>
    /// The Bannerlord engine executable. Under Steam the current process is the launcher that
    /// hosts the engine, so this resolves Bannerlord.exe from the same bin directory rather than
    /// re-launching the launcher (which would just show its menu, not boot the game).
    /// </summary>
    public static string GetEngineExecutablePath()
    {
        var binDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
        return Path.Combine(binDir, "Bannerlord.exe");
    }

    /// <summary>The ids of every currently active module, in load (dependency) order.</summary>
    public static string[] GetActiveModuleIds()
    {
        return ModuleHelper.GetActiveModules().Select(m => m.Id).ToArray();
    }
}
