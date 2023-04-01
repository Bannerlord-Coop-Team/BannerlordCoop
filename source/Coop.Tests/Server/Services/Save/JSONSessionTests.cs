using Common.Serialization;
using Coop.Core.Server.Services.Save.Data;
using GameInterface.Services.Save.Data;
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
        public void SaveSessions()
        {
            var gameObjectGuids = new GameObjectGuids(
                new Guid[] { Guid.NewGuid() },
                new Dictionary<string, Guid>
                {
                    { "Party 1", Guid.NewGuid() },
                    { "Party 2", Guid.NewGuid() },
                    { "Party 3", Guid.NewGuid() },
                },
                new Dictionary<string, Guid>
                {
                    { "Hero 1", Guid.NewGuid() },
                    { "Hero 2", Guid.NewGuid() },
                    { "Hero 3", Guid.NewGuid() },
                });

            ICoopSession sessionData = new CoopSession()
            {
                UniqueGameId = "TestId",
                GameObjectGuids = gameObjectGuids,
            };

            string saveFile = SAVE_PATH + sessionData.UniqueGameId + ".json";

            var options = new JsonSerializerOptions()
            {
                WriteIndented = true,
            };

            var fileIO = new JsonFileIO();

            fileIO.WriteToFile(saveFile, sessionData);

            string jsonString = JsonSerializer.Serialize(sessionData, options);

            Assert.NotEmpty(jsonString);
        }

        [Fact]
        public void SaveLoadSessions()
        {
            var gameObjectGuids = new GameObjectGuids(
                new Guid[] { Guid.NewGuid() },
                new Dictionary<string, Guid>
                {
                    { "Party 1", Guid.NewGuid() },
                    { "Party 2", Guid.NewGuid() },
                    { "Party 3", Guid.NewGuid() },
                },
                new Dictionary<string, Guid>
                {
                    { "Hero 1", Guid.NewGuid() },
                    { "Hero 2", Guid.NewGuid() },
                    { "Hero 3", Guid.NewGuid() },
                });

            ICoopSession sessionData = new CoopSession()
            {
                UniqueGameId = "TestId",
                GameObjectGuids = gameObjectGuids,
            };

            string saveFile = SAVE_PATH + sessionData.UniqueGameId + ".json";

            var fileIO = new JsonFileIO();

            fileIO.WriteToFile(saveFile, sessionData);

            var resolvedSessions = fileIO.ReadFromFile<CoopSession>(saveFile);

            Assert.NotNull(resolvedSessions);
            Assert.Equal(sessionData.UniqueGameId, resolvedSessions.UniqueGameId);
        }
    }
}
