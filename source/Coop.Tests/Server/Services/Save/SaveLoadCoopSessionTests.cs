using Autofac;
using Coop.Core;
using Coop.Core.Server;
using Coop.Core.Server.Services.Save;
using Coop.Core.Server.Services.Save.Data;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Data;
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
        private readonly ServerTestComponent serverComponent;
        private readonly IContainer container;

        public SaveLoadCoopSessionTests(ITestOutputHelper output)
        {
            this.output = output;

            serverComponent = new ServerTestComponent(output);

            container = serverComponent.Container; ;
        }

        [Fact]
        public void SaveSession()
        {
            // Setup
            var saveManager = container.Resolve<ICoopSaveManager>();
            var entityRegistry = container.Resolve<IControlledEntityRegistry>();

            const string controllerId = "testController";
            const string controller2Id = "testController2";
            const string entityId = "testEntity1";
            const string entity2Id = "testEntity2";

            Assert.True(entityRegistry.RegisterAsControlled(controllerId, entityId));
            Assert.True(entityRegistry.RegisterAsControlled(controller2Id, entity2Id));

            var entityMap = entityRegistry.PackageControlledEntities();

            ICoopSession sessionData = new CoopSession("SaveManagerTest", entityMap);

            string saveFile = sessionData.UniqueGameId;

            string savePath = saveManager.DefaultPath + saveFile;

            if (File.Exists(savePath + ".json"))
            {
                // Ensure file does not exist before testing
                File.Delete(savePath + ".json");
            }

            Assert.False(File.Exists(savePath));

            // Execution
            saveManager.SaveCoopSession(saveFile, sessionData);

            // Verification
            Assert.True(File.Exists(savePath + saveManager.FileType));
        }

        [Fact]
        public void SaveLoadSession()
        {
            // Setup
            var saveManager = container.Resolve<ICoopSaveManager>();
            var entityRegistry = container.Resolve<IControlledEntityRegistry>();

            const string controllerId = "testController";
            const string controller2Id = "testController2";
            const string entityId = "testEntity1";
            const string entity2Id = "testEntity2";

            Assert.True(entityRegistry.RegisterAsControlled(controllerId, entityId));
            Assert.True(entityRegistry.RegisterAsControlled(controller2Id, entity2Id));

            var entityMap = entityRegistry.PackageControlledEntities();

            ICoopSession sessionData = new CoopSession("SaveManagerTest", entityMap);

            string saveFile = SAVE_PATH + sessionData.UniqueGameId;

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
