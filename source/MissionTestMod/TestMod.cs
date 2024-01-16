using Autofac;
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
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Load;
using TaleWorlds.ScreenSystem;
using Module = TaleWorlds.MountAndBlade.Module;

namespace MissionTestMod
{
    public class TestMod : MBSubModuleBase
    {
        private readonly Harmony harmony = new Harmony("Coop.MissonTestMod");

        private static ILogger Logger;
        private static UpdateableList Updateables { get; } = new UpdateableList();
        private static InitialStateOption JoinTavern;
        private static InitialStateOption JoinArena;
        private static InitialStateOption JoinBattle;

        private IMissionGameManager gameManager;

        private IContainer container;

        protected override void OnSubModuleLoad()
        {
            AssemblyHellscape.CreateAssemblyBindingRedirects();

            SetupLogger();

            harmony.PatchAll(typeof(MissionModule).Assembly);

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

            JoinBattle = new InitialStateOption(
               "Join Online Battle",
               new TextObject("Join Online Battle"),
               9992,
               SelectSaveBattle,
               () => (false, new TextObject()));

            Module.CurrentModule.AddInitialStateOption(JoinTavern);
            Module.CurrentModule.AddInitialStateOption(JoinArena);
            // TODO re-enable for battles
            //Module.CurrentModule.AddInitialStateOption(JoinBattle);

            base.OnSubModuleLoad();
            Logger.Verbose("Bannerlord Coop Mod loaded");

            GameLoopRunner.Instance.SetGameLoopThread();
        }

        private void SetupLogger()
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
                    .WriteTo.File(filePath, outputTemplate: outputTemplate)
                    .MinimumLevel.Verbose();

            if (Debugger.IsAttached)
            {
                LogManager.Configuration.WriteTo.Debug(outputTemplate: outputTemplate);
            }
        }

        private void BuildContainer()
        {
            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<MissionModule>();

            container = builder.Build();

            ContainerProvider.SetContainer(container);
        }

        protected override void OnSubModuleUnloaded()
        {
            harmony.UnpatchAll();
            base.OnSubModuleUnloaded();
        }

        private void RegisterSurrogates()
        {
            Logger.Verbose("Registering ProtoBuf Surrogates");

            RuntimeTypeModel.Default.SetSurrogate<Vec3, Vec3Surrogate>();
            RuntimeTypeModel.Default.SetSurrogate<Vec2, Vec2Surrogate>();
            RuntimeTypeModel.Default.SetSurrogate<Mat3, Mat3Surrogate>();
            RuntimeTypeModel.Default.SetSurrogate<Blow, BlowSurrogate>();
            RuntimeTypeModel.Default.SetSurrogate<AttackCollisionData, AttackCollisionDataSurrogate>();
            RuntimeTypeModel.Default.SetSurrogate<CharacterObject, CharacterObjectSurrogate>();
            RuntimeTypeModel.Default.SetSurrogate<Banner, BannerSurrogate>();
            RuntimeTypeModel.Default.SetSurrogate<ItemObject, ItemObjectSurrogate>();
            RuntimeTypeModel.Default.SetSurrogate<ItemModifier, ItemModifierSurrogate>();
            RuntimeTypeModel.Default.SetSurrogate<Equipment, EquipmentSurrogate>();
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

        private void SelectSaveArena()
        {
            ScreenManager.PushScreen(ViewCreatorManager.CreateScreenView<MissionLoadGameGauntletScreen>(new object[]
                  {
                      new Action<SaveGameFileInfo>((SaveGameFileInfo saveGame)=>
                      {
                          SandBoxSaveHelper.TryLoadSave(saveGame, StartGameArena, null);
                      })
                  }));
        }


        private void SelectSaveTavern()
        {
            
            ScreenManager.PushScreen(ViewCreatorManager.CreateScreenView<MissionLoadGameGauntletScreen>(new object[]
                  {
                      new Action<SaveGameFileInfo>((SaveGameFileInfo saveGame)=>
                      {
                          SandBoxSaveHelper.TryLoadSave(saveGame, StartGameTavern, null);
                      })
                  }));
        }

        private void SelectSaveBattle()
        {

            ScreenManager.PushScreen(ViewCreatorManager.CreateScreenView<MissionLoadGameGauntletScreen>(new object[]
                  {
                      new Action<SaveGameFileInfo>((SaveGameFileInfo saveGame)=>
                      {
                          SandBoxSaveHelper.TryLoadSave(saveGame, StartGameBattle, null);
                      })
                  }));
        }

        private void StartGameTavern(LoadResult loadResult)
        {
            BuildContainer();
            gameManager = container.Resolve<TavernsGameManager>(new NamedParameter("loadedGameResult", loadResult));
            gameManager.StartGame();
        }

        private void StartGameArena(LoadResult loadResult)
        {
            BuildContainer();
            gameManager = container.Resolve<ArenaTestGameManager>(new NamedParameter("loadedGameResult", loadResult));
            gameManager.StartGame();
        }

        private void StartGameBattle(LoadResult loadResult)
        {
            BuildContainer();
            gameManager = container.Resolve<BattlesTestGameManager>(new NamedParameter("loadedGameResult", loadResult));
            gameManager.StartGame();
        }
    }
}
