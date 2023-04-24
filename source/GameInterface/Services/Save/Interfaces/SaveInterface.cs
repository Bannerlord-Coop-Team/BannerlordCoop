using Common.Logging;
using Common.Messaging;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Services.CharacterCreation.Messages;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Save;
using Serilog;
using System;
using System.Reflection;
using System.Security.Cryptography;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using TaleWorlds.SaveSystem;

namespace GameInterface.Services.Heroes.Interfaces
{
    internal interface ISaveInterface : IGameAbstraction
    {
        byte[] SaveCurrentGame();
    }

    internal class SaveInterface : ISaveInterface
    {
        private readonly ILogger Logger = LogManager.GetLogger<SaveInterface>();

        private static readonly MethodInfo SaveHandler_GetSaveMetaData = typeof(SaveHandler).GetMethod("GetSaveMetaData", BindingFlags.Public | BindingFlags.Instance);
        private static readonly MethodInfo MBSaveLoad_GetSaveMetaData = typeof(MBSaveLoad).GetMethod("GetSaveMetaData", BindingFlags.NonPublic | BindingFlags.Static);
        public byte[] SaveCurrentGame()
        {
            // Validation
            if (Game.Current == null) return ReportSaveFailure(nameof(Game.Current));
            if (Campaign.Current == null) return ReportSaveFailure(nameof(Campaign.Current));
            if (Campaign.Current.SaveHandler == null) return ReportSaveFailure(nameof(Campaign.Current.SaveHandler));

            // Logic
            var saveHandler = Campaign.Current.SaveHandler;
            var dataArgs = (CampaignSaveMetaDataArgs)SaveHandler_GetSaveMetaData.Invoke(saveHandler, Array.Empty<object>());
            var metaData = (MetaData)MBSaveLoad_GetSaveMetaData.Invoke(null, new object[] { dataArgs });

            var saveDriver = new CoopInMemSaveDriver();
            Game.Current.Save(metaData, "TransferSave", saveDriver, (SaveResult) => { });

            return saveDriver.Data;
        }

        /// <summary>
        /// Helper method for reporting save failures
        /// </summary>
        /// <param name="nullObjectName">Name of null object</param>
        /// <returns>Empty byte array</returns>
        private byte[] ReportSaveFailure(string nullObjectName)
        {
            Logger.Error($"Failed to package game save. {nullObjectName} was Null.");
            return Array.Empty<byte>();
        }
    }
}
