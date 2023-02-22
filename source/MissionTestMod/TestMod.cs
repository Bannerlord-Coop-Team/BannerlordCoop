using Common;
using Common.Logging;
using HarmonyLib;
using Missions;
using Missions.Services.Arena;
using Missions.Services.Network.Surrogates;
using Missions.Services.Taverns;
using Missions.View;
using ProtoBuf.Meta;
using SandBox;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using TaleWorlds.Engine;
using TaleWorlds.Engine.Options;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Load;
using TaleWorlds.ScreenSystem;

namespace MissionTestMod
{
    public class TestMod : MBSubModuleBase
    {
        private readonly Harmony harmony = new Harmony("Coop.MissonTestMod");

        private static ILogger Logger;
        private static UpdateableList Updateables { get; } = new UpdateableList();
        private static InitialStateOption JoinTavern;
        private static InitialStateOption JoinArena;
        private static InitialStateOption StartCoopServer;
        private static IMissionGameManager _gameManager;
        private static Process ServerProcess;

        protected override void OnSubModuleLoad()
        {
            if (Debugger.IsAttached)
            {
                var outputTemplate = "[({ProcessId}) {Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}";

                var filePath = $"Arena_Vertical_Slice_{Process.GetCurrentProcess().Id}.log";

                var dir = new DirectoryInfo(Directory.GetCurrentDirectory());

                // Delete all old log files
                foreach (var file in dir.EnumerateFiles("Arena_Vertical_Slice_*.log"))
                {
                    try
                    {
                        file.Delete();
                    }
                    catch (IOException) { }
                }

                LogManager.Configuration
                    .Enrich.WithProcessId()
                    .WriteTo.Debug(outputTemplate: outputTemplate)
                    .WriteTo.File(filePath, outputTemplate: outputTemplate)
                    .MinimumLevel.Verbose();
            }

            harmony.PatchAll();

            Logger = LogManager.GetLogger<TestMod>();
            RegisterSurrogates();

            Logger.Verbose("Building Network Configuration");

            Updateables.Add(GameLoopRunner.Instance);

            JoinTavern = new InitialStateOption(
              "Join Online Tavern",
              new TextObject("Join Online Tavern"),
              9991,
              SelectSaveTavern,
              () => (false, new TextObject()));

            JoinArena = new InitialStateOption(
               "Join Online Arena",
               new TextObject("Join Online Arena"),
               9990,
               SelectSaveArena,
               () => (false, new TextObject()));

            StartCoopServer = new InitialStateOption(
           "Start Coop Server",
           new TextObject("Start Coop Server"),
           9992,
           () =>
           {

               ScreenManager.PushScreen(ViewCreatorManager.CreateScreenView<MissionLoadGameGauntletScreen>(new object[]
                  {
                      new Action<SaveGameFileInfo>((SaveGameFileInfo saveGame)=>
                      {
                          StartCoopServerInstance();
                      })
                  }));


           },
            () => (false, new TextObject()));

            Module.CurrentModule.AddInitialStateOption(JoinTavern);
            Module.CurrentModule.AddInitialStateOption(JoinArena);
            Module.CurrentModule.AddInitialStateOption(StartCoopServer);


            base.OnSubModuleLoad();
            Logger.Verbose("Bannerlord Coop Mod loaded");
            
            if (Utilities.CommandLineArgumentExists("/headless"))
            {
                Mutex.TryOpenExisting("CoopServerReady", out Mutex mutex);
                mutex?.ReleaseMutex();
                System.Reflection.FieldInfo splashScreen = TaleWorlds.MountAndBlade.Module.CurrentModule.GetType().GetField("_splashScreenPlayed", 
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                splashScreen.SetValue(TaleWorlds.MountAndBlade.Module.CurrentModule, true);
                NativeOptions.SetConfig(NativeOptions.NativeOptionsType.MasterVolume, 0f);

                Utilities.ToggleRender();
                var client = new NamedPipeClientStream("PipesOfPiece");
                client.Connect();
                StreamReader reader = new StreamReader(client);
                StreamWriter writer = new StreamWriter(client);
                string saveIndex = reader.ReadLine();    
                writer.WriteLine("Ready");
                writer.Flush();

            }
        }

        private void StartCoopServerInstance()
        {
            var server = new NamedPipeServerStream("PipesOfPiece");
            StreamReader reader = new StreamReader(server);
            StreamWriter writer = new StreamWriter(server);

            Thread thread = new Thread(() =>
            {
                DisableSafeMode();
                StartServerProcess("/headless");
                server.WaitForConnection();
                writer.WriteLine("0");
                writer.Flush();
                reader.ReadLine();
                InformationManager.HideInquiry();
            });
            thread.IsBackground = true;
            thread.Start();
            InquiryData data = new InquiryData("Awaiting Server ",
                 "Awaiting for response from server. Please wait...", false, false, "", "", null, null);
            InformationManager.ShowInquiry(data);
        }


        private static void StartServerProcess(string additionalArgs)
        {
            if(ServerProcess == null) ServerProcess = new Process();
            ServerProcess.StartInfo.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
            ServerProcess.StartInfo.FileName = "\"" + AppDomain.CurrentDomain.BaseDirectory + @"\Bannerlord.exe" + "\"";
            ServerProcess.StartInfo.Arguments = $"/singleplayer {additionalArgs} _MODULES_*Native*SandBoxCore*CustomBattle*SandBox*StoryMode*MissionTestMod*_MODULES_";
            ServerProcess.StartInfo.CreateNoWindow = true;
            ServerProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            ServerProcess.Start();
        }

        private void DisableSafeMode()
        {
            string configFile = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), Utilities.GetApplicationName(), "Configs", "engine_config.txt");
            string[] lines = File.ReadAllLines(configFile);
            bool safetly_exited_changed = false;
            bool display_mode_changed = false;
            for (int i = 0; i < lines.Length; i++)
            {
                string[] parts = lines[i].Split('=');
                if (parts[0].Trim().Equals("safely_exited"))
                {
                    if (!parts[1].Trim().Equals("1"))
                    {
                        lines[i] = $"{parts[0]}= 1";
                    }
                    safetly_exited_changed = true;

                }
                if (parts[0].Trim().Equals("display_mode"))
                {
                    if (!parts[1].Trim().Equals("0"))
                    {
                        lines[i] = $"{parts[0]}=0";
                    }
                    display_mode_changed = true;

                }
                if(safetly_exited_changed && display_mode_changed)
                {
                    File.WriteAllLines(configFile, lines);
                    break; 
                }
                

            }

        }

        protected override void OnSubModuleUnloaded()
        {
            harmony.UnpatchAll();
            ServerProcess?.Kill();
            base.OnSubModuleUnloaded();
        }

        private void RegisterSurrogates()
        {
            Logger.Verbose("Registering ProtoBuf Surrogates");

            RuntimeTypeModel.Default.SetSurrogate<Vec3, Vec3Surrogate>();
            RuntimeTypeModel.Default.SetSurrogate<Vec2, Vec2Surrogate>();
        }

        private bool m_IsFirstTick = true;
        protected override void OnApplicationTick(float dt)
        {
            
            if (m_IsFirstTick)
            {
                GameLoopRunner.Instance.SetGameLoopThread();

                m_IsFirstTick = false;
            }
            TimeSpan frameTime = TimeSpan.FromSeconds(dt);
            Updateables.UpdateAll(frameTime);
        }

        private static void SelectSaveArena()
        {
            ScreenManager.PushScreen(ViewCreatorManager.CreateScreenView<MissionLoadGameGauntletScreen>(new object[]
                  {
                      new Action<SaveGameFileInfo>((SaveGameFileInfo saveGame)=>
                      {
                          SandBoxSaveHelper.TryLoadSave(saveGame, StartGameArena, null);
                      })
                  }));
        }


        private static void SelectSaveTavern()
        {
            ScreenManager.PushScreen(ViewCreatorManager.CreateScreenView<MissionLoadGameGauntletScreen>(new object[]
                  {
                      new Action<SaveGameFileInfo>((SaveGameFileInfo saveGame)=>
                      {
                          SandBoxSaveHelper.TryLoadSave(saveGame, StartGameTavern, null);
                      })
                  }));
        }

        private static void StartGameTavern(LoadResult loadResult)
        {
            _gameManager = new TavernsGameManager(loadResult);
            _gameManager.StartGame();
        }

        private static void StartGameArena(LoadResult loadResult)
        {
            _gameManager = new ArenaTestGameManager(loadResult);
            _gameManager.StartGame();
        }
    }
}
