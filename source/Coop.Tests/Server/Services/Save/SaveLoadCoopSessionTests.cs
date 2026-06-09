using Autofac;
using Coop.Core.Server.Services.Save;
using GameInterface.CoopSessionData.Save.Data;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Data;
using GameInterface.Services.Smithing;
using System;
using System.Collections.Generic;
using GameInterface.Services.Players.Data;
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
            // Use a real registry: the container's IControlledEntityRegistry is a shared test mock (see
            // TestComponentBase) whose RegisterAsControlled returns default(false). The save manager only needs
            // the packaged entity map, so a standalone real registry is sufficient and deterministic.
            var entityRegistry = new ControlledEntityRegistry();

            const string controllerId = "testController";
            const string controller2Id = "testController2";
            const string entityId = "testEntity1";
            const string entity2Id = "testEntity2";

            Assert.True(entityRegistry.RegisterAsControlled(controllerId, entityId));
            Assert.True(entityRegistry.RegisterAsControlled(controller2Id, entity2Id));

            var entityMap = entityRegistry.PackageControlledEntities();

            var players = new Player[]
            {
                new Player("PlayerHero","PlayerParty1"),
                new Player("PlayerHero2","PlayerParty2")
            };

            ICoopSession sessionData = new CoopSession("SaveManagerTest", entityMap, players, new CraftingPlayerData(new(), new(), new()));

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
            // Use a real registry: the container's IControlledEntityRegistry is a shared test mock (see
            // TestComponentBase) whose RegisterAsControlled returns default(false). The save manager only needs
            // the packaged entity map, so a standalone real registry is sufficient and deterministic.
            var entityRegistry = new ControlledEntityRegistry();

            const string controllerId = "testController";
            const string controller2Id = "testController2";
            const string entityId = "testEntity1";
            const string entity2Id = "testEntity2";

            Assert.True(entityRegistry.RegisterAsControlled(controllerId, entityId));
            Assert.True(entityRegistry.RegisterAsControlled(controller2Id, entity2Id));

            var entityMap = entityRegistry.PackageControlledEntities();

            var players = new Player[]
            {
                new Player("PlayerHero","PlayerParty1"),
                new Player("PlayerHero2","PlayerParty2")
            };

            ICoopSession sessionData = new CoopSession("SaveManagerTest", entityMap, players, new CraftingPlayerData(new(), new(), new()));

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
