using Common.Logging;
using Common.Serialization;
using GameInterface.CoopSessionData.Save.Data;
using Serilog;
using System;
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
                    return null;
                }

                // A file holding only a JSON null deserializes to null without throwing
                if (session == null)
                {
                    Logger.Error("Co-op session JSON at {FilePath} contains only null; saved co-op session data (player registrations and per-player data) will not be restored and the next save of {SaveName} replaces the file",
                        GetFullPathForLog(filePath), saveName);
                    return null;
                }

                // Session files from mod builds before Players existed still load, but without their registrations
                if (session.Players == null)
                {
                    Logger.Warning("Co-op session JSON at {FilePath} has no Players list; saved player registrations will not be restored and the next save of {SaveName} replaces the file",
                        GetFullPathForLog(filePath), saveName);
                    return session;
                }

                Logger.Information("Co-op session JSON loaded from {FilePath}: {PlayerCount} saved player registrations",
                    GetFullPathForLog(filePath), session.Players.Count(player => player != null));
                return session;
            }

            return null;
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
