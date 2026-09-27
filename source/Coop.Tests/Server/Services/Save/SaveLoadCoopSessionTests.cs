using Autofac;
using Common.Logging;
using Coop.Core.Server.Services.Save;
using GameInterface.CoopSessionData.Save.Data;
using GameInterface.Services.Alleys;
using GameInterface.Services.Caravans;
using GameInterface.Services.Heroes;
using GameInterface.Services.Inventory;
using GameInterface.Services.Heroes;
using System.Collections.Generic;
using GameInterface.Services.Inventory.TradeSkills;
using GameInterface.Services.MobileParties;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Smithing;
using GameInterface.Services.Workshops;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
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

            var interactionsPlayerData = new InteractionsPlayerData(new(), new(), new(), new(), new(), new(), new(), new(), new(), new(), new());
            interactionsPlayerData.PlayerAlreadySneakedSettlements[players[0].HeroId] = new() { "settlement1Id", "settlement2Id" };
            interactionsPlayerData.PlayerAlreadySneakedSettlements[players[1].HeroId] = new() { "settlement2Id", "settlement3Id" };
            interactionsPlayerData.PlayerOrderedDrinkThisDayInSettlement[players[0].HeroId] = "settlement1Id";
            interactionsPlayerData.PlayerOrderedDrinkThisDayInSettlement[players[1].HeroId] = "settlement2Id";
            interactionsPlayerData.PlayerHasBoughtTunToParty[players[0].HeroId] = true;
            interactionsPlayerData.PlayerHasBoughtTunToParty[players[1].HeroId] = false;
            interactionsPlayerData.PlayerHasMetRansomBroker[players[0].HeroId] = false;
            interactionsPlayerData.PlayerHasMetRansomBroker[players[1].HeroId] = true;

            var tradePlayerData = new TradePlayerData(new(), new(), new(), new());
            tradePlayerData.PlayerSettlementBribePaid[players[0].HeroId] = new() { ["settlement1Id"] = 0, ["settlement2Id"] = 1000 };
            tradePlayerData.PlayerSettlementBribePaid[players[1].HeroId] = new() { ["settlement2Id"] = 3200, ["settlement3Id"] = 123 };

            ICoopSession sessionData = new CoopSession(
                "SaveManagerTest",
                players,
                new CraftingPlayerData(new(), new(), new()),
                new WorkshopPlayerData(new()),
                new CaravansPlayerData(new(), new()),
                new AlleyPlayerData(new()),
                interactionsPlayerData,
                tradePlayerData,
                new InventoryPlayerData(new(), new()),
                new HeroMeetingData(new()),
                new AgingPlayerData(new()));

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

            var interactionsPlayerData = new InteractionsPlayerData(new(), new(), new(), new(), new(), new(), new(), new(), new(), new(), new());
            interactionsPlayerData.PlayerAlreadySneakedSettlements[players[0].HeroId] = new() { "settlement1Id", "settlement2Id" };
            interactionsPlayerData.PlayerAlreadySneakedSettlements[players[1].HeroId] = new() { "settlement2Id", "settlement3Id" };
            interactionsPlayerData.PlayerOrderedDrinkThisDayInSettlement[players[0].HeroId] = "settlement1Id";
            interactionsPlayerData.PlayerOrderedDrinkThisDayInSettlement[players[1].HeroId] = "settlement2Id";
            interactionsPlayerData.PlayerHasBoughtTunToParty[players[0].HeroId] = true;
            interactionsPlayerData.PlayerHasBoughtTunToParty[players[1].HeroId] = false;
            interactionsPlayerData.PlayerHasMetRansomBroker[players[0].HeroId] = false;
            interactionsPlayerData.PlayerHasMetRansomBroker[players[1].HeroId] = true;

            var tradePlayerData = new TradePlayerData(new(), new(), new(), new());
            tradePlayerData.PlayerSettlementBribePaid[players[0].HeroId] = new() { ["settlement1Id"] = 0, ["settlement2Id"] = 1000 };
            tradePlayerData.PlayerSettlementBribePaid[players[1].HeroId] = new() { ["settlement2Id"] = 3200, ["settlement3Id"] = 123 };

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
                tradePlayerData,
                new InventoryPlayerData(new(), new()),
                new HeroMeetingData(meetingTimes),
                new AgingPlayerData(new()));

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
                Assert.Equal(sessionData.InteractionsPlayerData.PlayerOrderedDrinkThisDayInSettlement[playerHeroId], savedSession.InteractionsPlayerData.PlayerOrderedDrinkThisDayInSettlement[playerHeroId]);
                Assert.Equal(sessionData.InteractionsPlayerData.PlayerHasBoughtTunToParty[playerHeroId], savedSession.InteractionsPlayerData.PlayerHasBoughtTunToParty[playerHeroId]);
                Assert.Equal(sessionData.InteractionsPlayerData.PlayerHasMetRansomBroker[playerHeroId], savedSession.InteractionsPlayerData.PlayerHasMetRansomBroker[playerHeroId]);

                Assert.Equal(sessionData.TradePlayerData.PlayerSettlementBribePaid[playerHeroId], savedSession.TradePlayerData.PlayerSettlementBribePaid[playerHeroId]);
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

        // Fixed so the kept copy's name, derived from the bad file's write time, is known up front
        private static readonly DateTime BadFileWriteTime = new DateTime(2026, 9, 27, 1, 2, 3, 456, DateTimeKind.Utc);
        private const string BadFileCopySuffix = ".json.20260927T010203456Z.unreadable";

        [Fact]
        public void LoadSession_NoFile_KeepsNoCopy()
        {
            var saveManager = container.Resolve<ICoopSaveManager>();
            string saveName = NewSaveName();

            try
            {
                ICoopSession? session = null;
                var logs = CaptureLogs(saveName, () => session = saveManager.LoadCoopSession(saveName));

                Assert.Null(session);
                Assert.Contains(logs, log => log.Contains("was not found"));
                Assert.Empty(FindCopies(saveManager, saveName));
            }
            finally
            {
                DeleteSessionFiles(saveManager, saveName);
            }
        }

        [Fact]
        public void LoadSession_TruncatedFile_LogsErrorAndKeepsCopy()
        {
            var saveManager = container.Resolve<ICoopSaveManager>();
            string saveName = NewSaveName();

            try
            {
                byte[] truncated = Encoding.UTF8.GetBytes("{\"UniqueGameId\": \"" + saveName + "\", \"Players\": [");
                string path = WriteSessionFile(saveManager, saveName, truncated);

                ICoopSession? session = null;
                var logs = CaptureLogs(saveName, () => session = saveManager.LoadCoopSession(saveName));

                Assert.Null(session);
                Assert.Contains(logs, log => log.Contains("could not be read") && log.Contains("JsonException"));
                Assert.Contains(logs, log => log.Contains("Kept a copy"));
                string copy = Assert.Single(FindCopies(saveManager, saveName));
                Assert.EndsWith(saveName + BadFileCopySuffix, copy);
                Assert.Equal(truncated, File.ReadAllBytes(copy));
                Assert.Equal(truncated, File.ReadAllBytes(path));
            }
            finally
            {
                DeleteSessionFiles(saveManager, saveName);
            }
        }

        [Fact]
        public void LoadSession_EmptyFile_LogsErrorAndKeepsCopy()
        {
            var saveManager = container.Resolve<ICoopSaveManager>();
            string saveName = NewSaveName();

            try
            {
                WriteSessionFile(saveManager, saveName, Array.Empty<byte>());

                ICoopSession? session = null;
                var logs = CaptureLogs(saveName, () => session = saveManager.LoadCoopSession(saveName));

                Assert.Null(session);
                Assert.Contains(logs, log => log.Contains("could not be read"));
                string copy = Assert.Single(FindCopies(saveManager, saveName));
                Assert.Empty(File.ReadAllBytes(copy));
            }
            finally
            {
                DeleteSessionFiles(saveManager, saveName);
            }
        }

        [Fact]
        public void LoadSession_JsonNull_LogsErrorAndKeepsCopy()
        {
            var saveManager = container.Resolve<ICoopSaveManager>();
            string saveName = NewSaveName();

            try
            {
                WriteSessionFile(saveManager, saveName, Encoding.UTF8.GetBytes("null"));

                ICoopSession? session = null;
                var logs = CaptureLogs(saveName, () => session = saveManager.LoadCoopSession(saveName));

                Assert.Null(session);
                Assert.Contains(logs, log => log.Contains("contains only null"));
                Assert.Single(FindCopies(saveManager, saveName));
            }
            finally
            {
                DeleteSessionFiles(saveManager, saveName);
            }
        }

        [Fact]
        public void LoadSession_OldSchemaWithoutPlayers_WarnsKeepsCopyAndReturnsSession()
        {
            var saveManager = container.Resolve<ICoopSaveManager>();
            string saveName = NewSaveName();

            try
            {
                // Shape written by mod builds that stored ControlledEntityMap instead of Players
                WriteSessionFile(saveManager, saveName, Encoding.UTF8.GetBytes("{\"UniqueGameId\": \"" + saveName + "\", \"ControlledEntityMap\": {}}"));

                ICoopSession? session = null;
                var logs = CaptureLogs(saveName, () => session = saveManager.LoadCoopSession(saveName));

                Assert.NotNull(session);
                Assert.Null(session.Players);
                Assert.Contains(logs, log => log.Contains("has no Players list"));
                Assert.Single(FindCopies(saveManager, saveName, ".noplayers"));
                Assert.Empty(FindCopies(saveManager, saveName));
            }
            finally
            {
                DeleteSessionFiles(saveManager, saveName);
            }
        }

        [Fact]
        public void LoadSession_EmptyPlayersArray_LogsLoadedLineAndKeepsNoCopy()
        {
            var saveManager = container.Resolve<ICoopSaveManager>();
            string saveName = NewSaveName();

            try
            {
                saveManager.SaveCoopSession(saveName, NewSession(saveName));

                ICoopSession? session = null;
                var logs = CaptureLogs(saveName, () => session = saveManager.LoadCoopSession(saveName));

                Assert.NotNull(session);
                Assert.Empty(session.Players);
                Assert.Contains(logs, log => log.Contains("0 saved player registrations"));
                Assert.DoesNotContain(logs, log => log.Contains("has no Players list"));
                Assert.Empty(FindCopies(saveManager, saveName));
            }
            finally
            {
                DeleteSessionFiles(saveManager, saveName);
            }
        }

        [Fact]
        public void LoadSession_FileLockedByAnotherHandle_LogsErrorWithoutThrowing()
        {
            var saveManager = container.Resolve<ICoopSaveManager>();
            string saveName = NewSaveName();

            try
            {
                string path = WriteSessionFile(saveManager, saveName, Encoding.UTF8.GetBytes("{\"Players\": []}"));

                ICoopSession? session = null;
                string[] logs;
                using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    logs = CaptureLogs(saveName, () => session = saveManager.LoadCoopSession(saveName));
                }

                Assert.Null(session);
                Assert.Contains(logs, log => log.Contains("could not be read") && log.Contains("IOException"));
                Assert.Contains(logs, log => log.Contains("Could not keep a copy"));
                Assert.Empty(FindCopies(saveManager, saveName));
            }
            finally
            {
                DeleteSessionFiles(saveManager, saveName);
            }
        }

        [Fact]
        public void LoadSession_SameUnreadableFileTwice_KeepsOneCopy()
        {
            var saveManager = container.Resolve<ICoopSaveManager>();
            string saveName = NewSaveName();

            try
            {
                WriteSessionFile(saveManager, saveName, Encoding.UTF8.GetBytes("{\"Players\": ["));

                saveManager.LoadCoopSession(saveName);
                var logs = CaptureLogs(saveName, () => saveManager.LoadCoopSession(saveName));

                Assert.Contains(logs, log => log.Contains("already kept"));
                Assert.Single(FindCopies(saveManager, saveName));
            }
            finally
            {
                DeleteSessionFiles(saveManager, saveName);
            }
        }

        [Fact]
        public void LoadSession_CopyNameTakenByDifferentFile_KeepsNumberedCopy()
        {
            var saveManager = container.Resolve<ICoopSaveManager>();
            string saveName = NewSaveName();

            try
            {
                byte[] truncated = Encoding.UTF8.GetBytes("{\"Players\": [");
                WriteSessionFile(saveManager, saveName, truncated);
                string takenPath = saveManager.DefaultPath + saveName + BadFileCopySuffix;
                byte[] existing = Encoding.UTF8.GetBytes("{\"Pla");
                File.WriteAllBytes(takenPath, existing);

                ICoopSession? session = null;
                var logs = CaptureLogs(saveName, () => session = saveManager.LoadCoopSession(saveName));

                Assert.Null(session);
                Assert.Equal(existing, File.ReadAllBytes(takenPath));
                string numberedPath = saveManager.DefaultPath + saveName + ".json.20260927T010203456Z.2.unreadable";
                Assert.Equal(truncated, File.ReadAllBytes(numberedPath));
                Assert.Contains(logs, log => log.Contains("Kept a copy") && log.Contains(".2.unreadable"));
            }
            finally
            {
                DeleteSessionFiles(saveManager, saveName);
            }
        }

        [Fact]
        public void SaveSession_AfterUnreadableLoad_LeavesCopyIntact()
        {
            var saveManager = container.Resolve<ICoopSaveManager>();
            string saveName = NewSaveName();

            try
            {
                byte[] truncated = Encoding.UTF8.GetBytes("{\"Players\": [");
                string path = WriteSessionFile(saveManager, saveName, truncated);
                saveManager.LoadCoopSession(saveName);

                saveManager.SaveCoopSession(saveName, NewSession(saveName));

                Assert.NotEqual(truncated, File.ReadAllBytes(path));
                string copy = Assert.Single(FindCopies(saveManager, saveName));
                Assert.Equal(truncated, File.ReadAllBytes(copy));
            }
            finally
            {
                DeleteSessionFiles(saveManager, saveName);
            }
        }

        [Fact]
        public void LoadSession_ValidFile_LogsLoadedLineWithPlayerCount()
        {
            var saveManager = container.Resolve<ICoopSaveManager>();
            string saveName = NewSaveName();

            try
            {
                var session = NewSession(saveName,
                    new Player("MyPlayer1", "MyHero1", "MyParty1", "MyClan1", "MyCharacter1"),
                    null,
                    new Player("MyPlayer2", "MyHero2", "MyParty2", "MyClan2", "MyCharacter2"));
                saveManager.SaveCoopSession(saveName, session);

                ICoopSession? loaded = null;
                var logs = CaptureLogs(saveName, () => loaded = saveManager.LoadCoopSession(saveName));

                Assert.NotNull(loaded);
                Assert.Contains(logs, log => log.Contains("loaded from") && log.Contains("2 saved player registrations"));
                Assert.Empty(FindCopies(saveManager, saveName));
            }
            finally
            {
                DeleteSessionFiles(saveManager, saveName);
            }
        }

        private static string NewSaveName() => "SessionLoadTest_" + Guid.NewGuid().ToString("N");

        private static ICoopSession NewSession(string saveName, params Player?[] players)
        {
            var empty = CoopSession.Empty;
            return new CoopSession(
                saveName,
                players,
                empty.CraftingPlayerData,
                empty.WorkshopPlayerData,
                empty.CaravansPlayerData,
                empty.AlleyPlayerData,
                empty.InteractionsPlayerData,
                empty.TradePlayerData,
                empty.InventoryPlayerData,
                empty.HeroMeetingData,
                empty.AgingPlayerData);
        }

        private static string WriteSessionFile(ICoopSaveManager saveManager, string saveName, byte[] contents)
        {
            Directory.CreateDirectory(saveManager.DefaultPath);
            string path = saveManager.DefaultPath + saveName + saveManager.FileType;
            File.WriteAllBytes(path, contents);
            File.SetLastWriteTimeUtc(path, BadFileWriteTime);
            return path;
        }

        private static string[] FindCopies(ICoopSaveManager saveManager, string saveName, string suffix = ".unreadable")
        {
            if (Directory.Exists(saveManager.DefaultPath) == false) return Array.Empty<string>();

            return Directory.GetFiles(saveManager.DefaultPath, saveName + ".json.*" + suffix);
        }

        // Other test classes log in parallel, so only lines naming this test's file are kept
        private static string[] CaptureLogs(string marker, Action action)
        {
            var messages = new ConcurrentQueue<string>();
            Action<string> callback = message =>
            {
                if (message.Contains(marker)) messages.Enqueue(message);
            };

            OutputSinkManager.AddLogCallback(callback);
            try
            {
                action();
            }
            finally
            {
                OutputSinkManager.RemoveLogCallback(callback);
            }

            return messages.ToArray();
        }

        private static void DeleteSessionFiles(ICoopSaveManager saveManager, string saveName)
        {
            if (Directory.Exists(saveManager.DefaultPath) == false) return;

            foreach (var file in Directory.GetFiles(saveManager.DefaultPath, saveName + "*"))
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }
        }
    }
}
