using Autofac;
using Coop.Core;
using Coop.Core.Server;
using Coop.Core.Server.Services.Save;
using Coop.Core.Server.Services.Save.Data;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Services.Save
{
    public class SaveLoadCoopSessionTests
    {
        private const string SAVE_PATH = "./";

        private readonly ITestOutputHelper output;
        private readonly IContainer container;

        public SaveLoadCoopSessionTests(ITestOutputHelper output)
        {
            this.output = output;

            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterModule<CoopModule>();
            builder.RegisterModule<ServerModule>();
            container = builder.Build();
        }

        [Fact]
        public void SaveSession()
        {
            // Setup
            var saveManager = container.Resolve<ICoopSaveManager>();
            

            ICoopSession sessionData = new CoopSession()
            {
                UniqueGameId = "SaveManagerTest",
                HeroStringIdToGuid = new Dictionary<string, Guid>
                {
                    { "Hero 1", Guid.NewGuid() },
                    { "Hero 2", Guid.NewGuid() },
                    { "Hero 3", Guid.NewGuid() },
                }
            };

            string saveFile = sessionData.UniqueGameId + ".json";

            string savePath = saveManager.DefaultPath + saveFile;

            if (File.Exists(savePath))
            {
                // Ensure file does not exist before testing
                File.Delete(savePath);
            }

            Assert.False(File.Exists(savePath));

            // Execution
            saveManager.SaveCoopSession(saveFile, sessionData);

            // Verification
            Assert.True(File.Exists(savePath));
        }

        [Fact]
        public void SaveLoadSession()
        {
            // Setup
            var saveManager = container.Resolve<ICoopSaveManager>();


            ICoopSession sessionData = new CoopSession()
            {
                UniqueGameId = "SaveLoadManagerTest",
                HeroStringIdToGuid = new Dictionary<string, Guid>
                {
                    { "Hero 1", Guid.NewGuid() },
                    { "Hero 2", Guid.NewGuid() },
                    { "Hero 3", Guid.NewGuid() },
                }
            };

            string saveFile = SAVE_PATH + sessionData.UniqueGameId + ".json";

            // Execution
            saveManager.SaveCoopSession(saveFile, sessionData);

            ICoopSession savedSession = saveManager.LoadCoopSession(saveFile);

            // Verification
            Assert.Equal(sessionData, savedSession);
        }

        [Fact]
        public void LoadSession_NoFile()
        {
            // Setup
            var saveManager = container.Resolve<ICoopSaveManager>();
            string saveFile = SAVE_PATH + "IDontExist.json";

            // Execution
            ICoopSession savedSession = saveManager.LoadCoopSession(saveFile);

            // Verification
            Assert.Null(savedSession);
        }
    }
}
