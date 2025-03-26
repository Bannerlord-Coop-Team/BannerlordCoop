using Common.Serialization;
using Coop.Core.Server.Services.Save.Data;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Data;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace Coop.Tests.Server.Services.Save
{
    public class JSONSessionTests
    {
        private const string SAVE_PATH = "./saves/";

        private readonly ITestOutputHelper output;

        public JSONSessionTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void SaveLoadSessions()
        {
            var gameObjectGuids = new GameObjectGuids(new string[] { "Random STR" });

            var entityRegistry = new ControlledEntityRegistry();

            const string controllerId = "testController";
            const string entityId = "testEntity1";

            Assert.True(entityRegistry.RegisterAsControlled(controllerId, entityId));

            var sessionData = new CoopSession("TestId", entityRegistry.PackageControlledEntities());

            string saveFile = SAVE_PATH + sessionData.UniqueGameId + ".json";

            var fileIO = new JsonFileIO();

            fileIO.WriteToFile(saveFile, sessionData);

            var resolvedSessions = fileIO.ReadFromFile<CoopSession>(saveFile);

            Assert.NotNull(resolvedSessions);
            Assert.Equal(sessionData.UniqueGameId, resolvedSessions.UniqueGameId);
        }
    }
}
