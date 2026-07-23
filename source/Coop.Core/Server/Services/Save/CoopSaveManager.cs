using Common.Serialization;
using GameInterface.CoopSessionData.Save.Data;
using System;
using System.IO;
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
        public string DefaultPath { get; } = ResolveDefaultPath();
        public string FileType { get; } = ".json";

        /// <summary>
        /// The session json (player→hero mappings + per-player session data) must persist next to
        /// the campaign saves so a save transfers as one folder's &lt;name&gt;.sav + &lt;name&gt;.json pair.
        /// The graphical host resolves the native save folder through the engine's platform helper
        /// (Documents\Mount and Blade II Bannerlord\Game Saves — the same PlatformFileType.User +
        /// "Game Saves" root FileDriver writes .sav files to). Headless and container hosts set
        /// BANNERLORD_USER_DIR — the persistent data root, mounted as /data in Docker — and MUST
        /// store the session there: written CWD-relative in a container it lands in the ephemeral
        /// layer, evaporates on recreate, and returning players lose their heroes. Without either
        /// (unit tests, engine not booted) the CWD-relative ./saves/ is used.
        /// </summary>
        private static string ResolveDefaultPath()
        {
            var userDir = Environment.GetEnvironmentVariable("BANNERLORD_USER_DIR");
            if (string.IsNullOrEmpty(userDir) == false)
                return Path.Combine(userDir, "saves") + Path.DirectorySeparatorChar;

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
                try
                {
                    var fileIO = new JsonFileIO();
                    return fileIO.ReadFromFile<CoopSession>(filePath);
                }
                catch (Exception)
                {
                    return null;
                }
            }

            return null;
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
