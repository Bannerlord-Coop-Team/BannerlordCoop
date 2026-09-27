using Common.Logging;
using Common.Serialization;
using GameInterface.CoopSessionData.Save.Data;
using Serilog;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using TaleWorlds.Library;

namespace Coop.Core.Server.Services.Save
{
    internal interface ICoopSaveManager
    {
        string DefaultPath { get; }
        string FileType { get; }
        void SaveCoopSession(string saveName, ICoopSession session);

        ICoopSession LoadCoopSession(string saveName);
    }

    internal class CoopSaveManager : ICoopSaveManager
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CoopSaveManager>();

        public string DefaultPath { get; } = ResolveDefaultPath();
        public string FileType { get; } = ".json";

        /// <summary>
        /// The session json (player→hero mappings + per-player session data) must persist next to
        /// the campaign saves so a save moves or deletes as one folder's &lt;name&gt;.sav +
        /// &lt;name&gt;.json pair. The graphical host resolves the native save folder through the
        /// engine's platform helper (Documents\Mount and Blade II Bannerlord\Game Saves — the same
        /// PlatformFileType.User + "Game Saves" root FileDriver writes .sav files to). Headless and
        /// container hosts set BANNERLORD_USER_DIR — the persistent data root, mounted as /data in
        /// Docker — and store the session in ITS "Game Saves" folder: that is where the host's
        /// redirected FileDriver puts the .sav files, and written CWD-relative instead it lands in
        /// a container's ephemeral layer, evaporates on recreate, and returning players lose their
        /// heroes. (The dedicated server migrates jsons from the pre-pairing &lt;root&gt;\saves\
        /// location at boot — its RepairSave.) Without either (unit tests, engine not booted) the
        /// CWD-relative ./saves/ is used.
        /// </summary>
        private static string ResolveDefaultPath()
        {
            var userDir = Environment.GetEnvironmentVariable("BANNERLORD_USER_DIR");
            if (string.IsNullOrEmpty(userDir) == false)
                return Path.Combine(userDir, "Game Saves") + Path.DirectorySeparatorChar;

            if (TaleWorlds.Library.Common.PlatformFileHelper is PlatformFileHelperPC fileHelper)
            {
                var nativeSaveDir = new PlatformDirectoryPath(PlatformFileType.User, "Game Saves" + Path.DirectorySeparatorChar);
                return fileHelper.GetDirectoryFullPath(nativeSaveDir);
            }

            return "./saves/";
        }

        /// <summary>
        /// Loads a CoopSession from the provided file name.
        /// File name must include '.json' ending
        /// </summary>
        /// <param name="saveName">File to load session from</param>
        /// <returns>Loaded session if found, otherwise null</returns>
        public ICoopSession LoadCoopSession(string saveName)
        {
            string filePath = string.Concat(DefaultPath, saveName, FileType);

            if (File.Exists(filePath))
            {
                CoopSession session;
                try
                {
                    var fileIO = new JsonFileIO();
                    session = fileIO.ReadFromFile<CoopSession>(filePath);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Co-op session JSON at {FilePath} could not be read; saved co-op session data (player registrations and per-player data) will not be restored and the next save of {SaveName} replaces the file",
                        GetFullPathForLog(filePath), saveName);
                    KeepSessionCopy(filePath, UnreadableCopySuffix);
                    return null;
                }

                // A file holding only a JSON null deserializes to null without throwing
                if (session == null)
                {
                    Logger.Error("Co-op session JSON at {FilePath} contains only null; saved co-op session data (player registrations and per-player data) will not be restored and the next save of {SaveName} replaces the file",
                        GetFullPathForLog(filePath), saveName);
                    KeepSessionCopy(filePath, UnreadableCopySuffix);
                    return null;
                }

                // Session files from mod builds before Players existed still load, but without their registrations
                if (session.Players == null)
                {
                    Logger.Warning("Co-op session JSON at {FilePath} has no Players list; saved player registrations will not be restored and the next save of {SaveName} replaces the file",
                        GetFullPathForLog(filePath), saveName);
                    KeepSessionCopy(filePath, NoPlayersCopySuffix);
                    return session;
                }

                Logger.Information("Co-op session JSON loaded from {FilePath}: {PlayerCount} saved player registrations",
                    GetFullPathForLog(filePath), session.Players.Count(player => player != null));
                return session;
            }

            Logger.Warning("Co-op session JSON was not found at {FilePath}; saved player registrations will not be restored",
                GetFullPathForLog(filePath));
            return null;
        }

        private const string UnreadableCopySuffix = ".unreadable";
        private const string NoPlayersCopySuffix = ".noplayers";
        private const int MaxSessionCopyNames = 5;

        // Named after the file's own write time, so restarts on the same file reuse one copy
        private static void KeepSessionCopy(string filePath, string suffix)
        {
            string copyPath = null;
            try
            {
                var source = new FileInfo(filePath);
                if (source.Exists == false)
                {
                    Logger.Warning("Co-op session JSON {FilePath} disappeared before a copy could be kept", GetFullPathForLog(filePath));
                    return;
                }

                string copyStem = string.Concat(filePath, ".", source.LastWriteTimeUtc.ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture));
                for (int attempt = 1; attempt <= MaxSessionCopyNames; attempt++)
                {
                    copyPath = attempt == 1
                        ? copyStem + suffix
                        : string.Concat(copyStem, ".", attempt.ToString(CultureInfo.InvariantCulture), suffix);

                    if (File.Exists(copyPath))
                    {
                        if (FilesMatch(filePath, copyPath))
                        {
                            Logger.Warning("Co-op session JSON {FilePath} is already kept at {CopyPath}", GetFullPathForLog(filePath), GetFullPathForLog(copyPath));
                            return;
                        }

                        // A different file holds this name (a partial copy, or another file with the same write time)
                        continue;
                    }

                    try
                    {
                        File.Copy(filePath, copyPath, false);
                    }
                    catch (IOException) when (FilesMatch(filePath, copyPath))
                    {
                        // Another load kept the same file between the check and the copy
                    }

                    Logger.Warning("Kept a copy of co-op session JSON {FilePath} at {CopyPath}", GetFullPathForLog(filePath), GetFullPathForLog(copyPath));
                    return;
                }

                Logger.Error("Could not keep a copy of co-op session JSON {FilePath}: {Count} different files already use its copy names; back it up by hand before the next save replaces it",
                    GetFullPathForLog(filePath), MaxSessionCopyNames);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Could not keep a copy of co-op session JSON {FilePath} at {CopyPath}; back it up by hand before the next save replaces it",
                    GetFullPathForLog(filePath), GetFullPathForLog(copyPath));
            }
        }

        private static bool FilesMatch(string path, string otherPath)
        {
            var other = new FileInfo(otherPath);
            if (other.Exists == false || other.Length != new FileInfo(path).Length) return false;

            return File.ReadAllBytes(path).SequenceEqual(File.ReadAllBytes(otherPath));
        }

        // .NET Framework throws on path characters .NET Core accepts, and a log line must not throw
        private static string GetFullPathForLog(string path)
        {
            try
            {
                return Path.GetFullPath(path);
            }
            catch (Exception)
            {
                return path;
            }
        }

        /// <summary>
        /// Saves the given coop session to a json file
        /// File name must include '.json' fine ending.
        /// </summary>
        /// <param name="saveName">File name to save to <see cref="DefaultPath"/>.</param>
        /// <param name="session">Session to save</param>
        public void SaveCoopSession(string saveName, ICoopSession session)
        {
            string filePath = string.Concat(DefaultPath, saveName, FileType);

            var fileIO = new JsonFileIO();

            fileIO.WriteToFile(filePath, session);
        }
    }
}
