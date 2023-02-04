﻿using Common;
using Common.Logging;
using HarmonyLib;
using Missions;
using Missions.Services.Arena;
using Missions.Services.Network.Surrogates;
using Missions.Services.Taverns;
using ProtoBuf.Meta;
using SandBox;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Load;

namespace MissionTestMod
{
    public class TestMod : MBSubModuleBase
    {
        private readonly Harmony harmony = new Harmony("Coop.MissonTestMod");

        private static ILogger Logger;
		private static UpdateableList Updateables { get; } = new UpdateableList();
        private static InitialStateOption JoinTavern;
        private static InitialStateOption JoinArena;
        private static IMissionGameManager _gameManager;

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
              StartClientInTavern,
              () => (false, new TextObject()));

            JoinArena = new InitialStateOption(
                "Join Online Arena",
                 new TextObject("Join Online Arena"),
                 9990,
                 StartClientInArena,
                 () => (false, new TextObject()));

            Module.CurrentModule.AddInitialStateOption(JoinTavern);
            Module.CurrentModule.AddInitialStateOption(JoinArena);


            base.OnSubModuleLoad();
            Logger.Verbose("Bannerlord Coop Mod loaded");
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

        private static void StartClientInTavern()
        {
	        SaveGameFileInfo[] saveFiles = MBSaveLoad.GetSaveFiles(null);
            SaveGameFileInfo save = saveFiles.FirstOrDefault(s => ValidateModules(s.MetaData));

            if(save == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("Unable to find a save without Mods. Create a fresh game and try again."));
            }
            else
            {
                SandBoxSaveHelper.TryLoadSave(save, StartGameTavern, null);
            }
        }

        private static void StartClientInArena()
        {
            SaveGameFileInfo[] saveFiles = MBSaveLoad.GetSaveFiles(null);
            SaveGameFileInfo save = saveFiles.FirstOrDefault(s => ValidateModules(s.MetaData));

            if (save == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("Unable to find a save without Mods. Create a fresh game and try again."));
            }
            else
            {
                SandBoxSaveHelper.TryLoadSave(save, StartGameArena, null);
            }
        }

        private static readonly HashSet<string> allowedModules = new HashSet<string>()
        {
            "Native",
            "Sandbox",
            "SandBox Core",
            "StoryMode",
            "CustomBattle",
            "BirthAndDeath",
            "MissionTestMod",
        };
        private static bool ValidateModules(MetaData metaData)
        {
            if(metaData == null) return false;

            var moduleNames = metaData.GetModules();
            if (moduleNames.Any(name => !allowedModules.Contains(name))) return false;
            
            return true;
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
