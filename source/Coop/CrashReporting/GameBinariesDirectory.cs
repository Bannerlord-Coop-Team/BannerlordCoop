using System;
using System.IO;
using System.Security;
using TaleWorlds.Library;

namespace Coop.CrashReporting
{
    /// <summary>
    /// Resolves the directory the running game's binaries live in (bin/Win64_Shipping_Client) so the
    /// crash reporter can record which files and versions were installed. The resolution has to happen
    /// in the module: the reporter is a plain process with no TaleWorlds assemblies of its own.
    /// </summary>
    internal static class GameBinariesDirectory
    {
        /// <summary>
        /// The full path of the game's binaries directory, or null when it cannot be resolved.
        /// </summary>
        internal static string Resolve()
        {
            // BasePath.Name is the install root (a path relative to the bin directory on Windows) and
            // Common.ConfigName is the platform's bin folder name, e.g. Win64_Shipping_Client. Fully
            // qualified because the module's own Common assembly shadows TaleWorlds.Library.Common.
            string resolved = TryGetFullPath(Path.Combine(
                BasePath.Name,
                "bin",
                TaleWorlds.Library.Common.ConfigName));
            if (resolved != null && Directory.Exists(resolved))
                return resolved;

            // Headless and test hosts do not always run out of the game's bin directory, in which case
            // the executable's own directory is the honest answer.
            string executableDirectory = TryGetFullPath(AppDomain.CurrentDomain.BaseDirectory);
            return executableDirectory != null && Directory.Exists(executableDirectory)
                ? executableDirectory
                : null;
        }

        private static string TryGetFullPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            try
            {
                return Path.GetFullPath(path);
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is NotSupportedException ||
                exception is PathTooLongException ||
                exception is IOException ||
                exception is SecurityException)
            {
                return null;
            }
        }
    }
}
