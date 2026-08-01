using GameInterface.Services.Modules;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using TaleWorlds.ModuleManager;
using ModuleInfo = GameInterface.Services.Modules.ModuleInfo;

namespace GameInterface.Services.GameState;

/// <summary>
/// Resolves how to spawn the server process a Host click creates: preferably the dedicated
/// server shipped inside the Coop module's DedicatedServer folder, otherwise the game engine
/// executable with the active module list, both read from the running game so the spawned
/// server matches the hosting client.
/// </summary>
public static class ManagedServerLauncher
{
    /// <summary>The dedicated-server deployment folder nested inside the Coop module.</summary>
    public const string DedicatedServerFolderName = "DedicatedServer";

    /// <summary>The dedicated-server launcher executable at that deployment's root.</summary>
    public const string DedicatedServerExecutableName = "BannerlordCoopServer.exe";

    /// <summary>The id of this mod's module, whose folder carries the dedicated server.</summary>
    public const string CoopModuleId = "Coop";

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

    /// <summary>
    /// The dedicated server bundled with the installed Coop module, or null when the module
    /// does not carry one (the Host flow then falls back to spawning the game engine).
    /// </summary>
    public static string GetDedicatedServerExecutablePath()
    {
        string coopModuleRoot;
        try
        {
            coopModuleRoot = ModuleHelper.GetModuleFullPath(CoopModuleId);
        }
        catch (Exception)
        {
            // An unresolvable module path only ever means "no dedicated server here".
            return null;
        }

        return ResolveDedicatedServerExecutable(coopModuleRoot);
    }

    /// <summary>
    /// Resolves <see cref="DedicatedServerExecutableName"/> inside the module's
    /// <see cref="DedicatedServerFolderName"/> folder; null when it is not installed there.
    /// </summary>
    public static string ResolveDedicatedServerExecutable(string coopModuleRoot)
    {
        if (string.IsNullOrEmpty(coopModuleRoot)) return null;

        string executablePath = Path.Combine(
            coopModuleRoot, DedicatedServerFolderName, DedicatedServerExecutableName);

        return File.Exists(executablePath) ? executablePath : null;
    }

    /// <summary>
    /// Whether the dedicated server can host the currently active module set. Its module list
    /// is pinned, so a session with extra community modules must be hosted by the game engine
    /// instead — the dedicated server could not load them and module validation would then
    /// reject every joining client that has them enabled.
    /// </summary>
    public static bool CanDedicatedServerHostActiveModules()
    {
        return CanDedicatedServerHostModules(CompatibilityInfo.Get().Modules);
    }

    /// <summary>
    /// True when every module is loadable by the dedicated server's pinned module list:
    /// official non-DLC game modules, this mod itself, and the DedicatedServer.* host modules
    /// (the same set <see cref="Modules.Validators.ModuleValidator"/> exempts from matching).
    /// DLC is official but not part of that pinned list, so it disqualifies the dedicated server.
    /// </summary>
    public static bool CanDedicatedServerHostModules(IEnumerable<ModuleInfo> modules)
    {
        if (modules == null) return false;

        return modules.All(module =>
            (module.IsOfficial && !module.IsDlc) ||
            string.Equals(module.Id, CoopModuleId, StringComparison.OrdinalIgnoreCase) ||
            (module.Id != null &&
                module.Id.StartsWith("DedicatedServer.", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>The ids of every currently active module, in load (dependency) order.</summary>
    public static string[] GetActiveModuleIds()
    {
        return ModuleHelper.GetActiveModules().Select(m => m.Id).ToArray();
    }
}
