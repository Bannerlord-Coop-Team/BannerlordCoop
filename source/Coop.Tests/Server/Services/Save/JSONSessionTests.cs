using Common.Serialization;
using GameInterface.CoopSessionData.Save.Data;
using GameInterface.Services.Caravans;
using GameInterface.Services.Heroes.Data;
using GameInterface.Services.MobileParties;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Smithing;
using GameInterface.Services.Workshops;
using GameInterface.Services.Alleys;
using ProtoBuf;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;
using GameInterface.Services.Inventory.TradeSkills;

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

            var players = new Player[]
            {
                new Player("MyPlayer", "MyHero", "MyParty", "MyClan", "MyCharacter")
            };
            var workshopData = new WorkshopDataSnapshot(
                isGettingInputsFromWarehouse: true,
                productionProgressForWarehouse: 0.25f,
                productionProgressForTown: 0.5f,
                stockProductionInWarehouseRatio: 0.75f);

            var sessionData = new CoopSession(
                "TestId",
                players,
                new CraftingPlayerData(new(), new(), new()),
                new WorkshopPlayerData(
                    new(),
                    new() { ["Workshop_1"] = workshopData }),
                new CaravansPlayerData(new(), new()),
                new AlleyPlayerData(new()),
                new InteractionsPlayerData(new(), new(), new(), new()),
                new TradePlayerData(new()));

            string saveFile = SAVE_PATH + sessionData.UniqueGameId + ".json";

            var fileIO = new JsonFileIO();

            fileIO.WriteToFile(saveFile, sessionData);

            var resolvedSessions = fileIO.ReadFromFile<CoopSession>(saveFile);

            Assert.NotNull(resolvedSessions);
            Assert.Equal(sessionData.UniqueGameId, resolvedSessions.UniqueGameId);
            WorkshopDataSnapshot resolvedWorkshopData =
                Assert.Contains("Workshop_1", resolvedSessions.WorkshopPlayerData.WorkshopDataByWorkshopId);
            Assert.True(resolvedWorkshopData.IsGettingInputsFromWarehouse);
            Assert.Equal(0.25f, resolvedWorkshopData.ProductionProgressForWarehouse);
            Assert.Equal(0.5f, resolvedWorkshopData.ProductionProgressForTown);
            Assert.Equal(0.75f, resolvedWorkshopData.StockProductionInWarehouseRatio);
        }

        [Fact]
        public void WorkshopPlayerData_OldJsonWithoutWorkshopData_UsesEmptySnapshot()
        {
            const string oldJson = "{\"PlayerWarehouseRosterPerSettlement\":{}}";

            WorkshopPlayerData workshopPlayerData = JsonSerializer.Deserialize<WorkshopPlayerData>(
                oldJson,
                new JsonSerializerOptions { IncludeFields = true });

            Assert.NotNull(workshopPlayerData);
            Assert.Empty(workshopPlayerData.WorkshopDataByWorkshopId);
        }

        [Fact]
        public void WorkshopPlayerData_ProtoBufRoundTrip_PreservesWorkshopSnapshot()
        {
            var workshopPlayerData = new WorkshopPlayerData(
                new(),
                new()
                {
                    ["Workshop_1"] = new WorkshopDataSnapshot(
                        isGettingInputsFromWarehouse: true,
                        productionProgressForWarehouse: 0.25f,
                        productionProgressForTown: 0.5f,
                        stockProductionInWarehouseRatio: 0.75f),
                });

            WorkshopPlayerData resolvedWorkshopPlayerData = Serializer.DeepClone(workshopPlayerData);
            WorkshopDataSnapshot resolvedWorkshopData =
                Assert.Contains("Workshop_1", resolvedWorkshopPlayerData.WorkshopDataByWorkshopId);

            Assert.True(resolvedWorkshopData.IsGettingInputsFromWarehouse);
            Assert.Equal(0.25f, resolvedWorkshopData.ProductionProgressForWarehouse);
            Assert.Equal(0.5f, resolvedWorkshopData.ProductionProgressForTown);
            Assert.Equal(0.75f, resolvedWorkshopData.StockProductionInWarehouseRatio);
        }
    }
}
