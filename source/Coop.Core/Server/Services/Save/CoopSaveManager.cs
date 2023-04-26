using Common.Messaging;
using Common.Serialization;
using Coop.Core.Server.Services.Save.Data;
using System;
using System.IO;

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
        public string DefaultPath { get; } = "./Saves/";
        public string FileType { get; } = ".json";

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
                var fileIO = new JsonFileIO();

                return fileIO.ReadFromFile<CoopSession>(filePath);
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
