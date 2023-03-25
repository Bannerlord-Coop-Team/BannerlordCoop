using Common.Messaging;
using Common.Serialization;
using Coop.Core.Server.Services.Save.Data;
using System.IO;

namespace Coop.Core.Server.Services.Save
{
    internal interface ICoopSaveManager
    {
        void SaveCoopSession(string saveName, ICoopSession session);
        ICoopSession LoadCoopSession(string saveName);
    }

    internal class CoopSaveManager : ICoopSaveManager
    {
        private const string SAVE_PATH = "./Saves/";

        public ICoopSession LoadCoopSession(string saveName)
        {
            string filePath = string.Concat(SAVE_PATH, saveName);

            if (File.Exists(filePath))
            {
                var fileIO = new JsonFileIO();

                return fileIO.ReadFromFile<CoopSession>(filePath);
            }

            return null;
        }

        public void SaveCoopSession(string saveName, ICoopSession session)
        {
            string filePath = string.Concat(SAVE_PATH, saveName);

            var fileIO = new JsonFileIO();

            fileIO.WriteToFile(filePath, session);
        }
    }
}
