using Autofac;
using Coop.Core.Server.Services.Save;
using GameInterface.CoopSessionData.Save.Data;
using GameInterface.Services.Alleys;
using GameInterface.Services.Caravans;
using GameInterface.Services.Inventory;
using GameInterface.Services.Heroes;
using System.Collections.Generic;
using GameInterface.Services.Inventory.TradeSkills;
using GameInterface.Services.MobileParties;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Smithing;
using GameInterface.Services.Workshops;
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

            var players = new Player[]
            {
                new Player("MyPlayer1", "MyHero1","MyParty1", "MyClan1", "MyCharacter1"),
                new Player("MyPlayer2", "MyHero2","MyParty2", "MyClan2", "MyCharacter2"),
            };

            var interactionsPlayerData = new InteractionsPlayerData(new(), new(), new(), new(), new(), new(), new(), new());
            interactionsPlayerData.PlayerAlreadySneakedSettlements[players[0].HeroId] = new() { "settlement1Id", "settlement2Id" };
            interactionsPlayerData.PlayerAlreadySneakedSettlements[players[1].HeroId] = new() { "settlement2Id", "settlement3Id" };

            ICoopSession sessionData = new CoopSession(
                "SaveManagerTest",
                players,
                new CraftingPlayerData(new(), new(), new()),
                new WorkshopPlayerData(new()),
                new CaravansPlayerData(new(), new()),
                new AlleyPlayerData(new()),
                interactionsPlayerData,
                new TradePlayerData(new(), new(), new()),
                new InventoryPlayerData(new(), new()),
                new HeroMeetingData(new()));

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

            var players = new Player[]
            {
                new Player("MyPlayer1", "MyHero1","MyParty1", "MyClan1", "MyCharacter1"),
                new Player("MyPlayer2", "MyHero2","MyParty2", "MyClan2", "MyCharacter2"),
            };

            var interactionsPlayerData = new InteractionsPlayerData(new(), new(), new(), new(), new(), new(), new(), new());
            interactionsPlayerData.PlayerAlreadySneakedSettlements[players[0].HeroId] = new() { "settlement1Id", "settlement2Id" };
            interactionsPlayerData.PlayerAlreadySneakedSettlements[players[1].HeroId] = new() { "settlement2Id", "settlement3Id" };

            var meetingTimes = new Dictionary<string, Dictionary<string, long>>
            {
                ["MyHero1"] = new Dictionary<string, long>
                {
                    ["lord_6_1"] = 1351,
                },
            };

            ICoopSession sessionData = new CoopSession(
                "SaveManagerTest",
                players,
                new CraftingPlayerData(new(), new(), new()),
                new WorkshopPlayerData(new()),
                new CaravansPlayerData(new(), new()),
                new AlleyPlayerData(new()),
                interactionsPlayerData,
                new TradePlayerData(new(), new(), new()),
                new InventoryPlayerData(new(), new()),
                new HeroMeetingData(meetingTimes));

            string saveFile = SAVE_PATH + sessionData.UniqueGameId;

            // Execution
            saveManager.SaveCoopSession(saveFile, sessionData);

            ICoopSession savedSession = saveManager.LoadCoopSession(saveFile);

            // Verification
            // CoopSession/Player are plain data classes without value equality, so the round-tripped
            // session is compared field-by-field rather than via whole-object Assert.Equal.
            Assert.NotNull(savedSession);
            Assert.Equal(sessionData.UniqueGameId, savedSession.UniqueGameId);
            Assert.Equal(sessionData.Players.Length, savedSession.Players.Length);
            for (int i = 0; i < sessionData.Players.Length; i++)
            {
                Assert.Equal(sessionData.Players[i].ControllerId, savedSession.Players[i].ControllerId);
                Assert.Equal(sessionData.Players[i].HeroId, savedSession.Players[i].HeroId);
                Assert.Equal(sessionData.Players[i].MobilePartyId, savedSession.Players[i].MobilePartyId);
                Assert.Equal(sessionData.Players[i].ClanId, savedSession.Players[i].ClanId);

                var playerHeroId = sessionData.Players[i].HeroId;

                Assert.Equal(sessionData.InteractionsPlayerData.PlayerAlreadySneakedSettlements[playerHeroId], savedSession.InteractionsPlayerData.PlayerAlreadySneakedSettlements[playerHeroId]);
            }
            Assert.Equal(1351, savedSession.HeroMeetingData.PlayerLastMeetingTimes["MyHero1"]["lord_6_1"]);
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
