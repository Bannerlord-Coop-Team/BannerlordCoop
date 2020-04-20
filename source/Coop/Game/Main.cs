using Coop.Common;
using Coop.Game.Debug;
using HarmonyLib;
using System;
using System.IO;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace Coop.Game
{
    class Main : NoHarmony.NoHarmonyLoader
    {
        public static Main Instance;
        
        public Main()
        {
            Instance = this;
            m_Updateables = new UpdateableList();
        }
        public override void NoHarmonyInit()
        {
            initLogger();
        }

        public override void NoHarmonyLoad()
        {
            AddModel<ClientModel>();

            var harmony = new Harmony("com.TaleWorlds.MountAndBlade.Bannerlord");
            harmony.PatchAll();

            Module.CurrentModule.AddInitialStateOption(new InitialStateOption("Check server status.",
             new TextObject("Check server status.", null),
             9990,
             () =>
             {
                 if(CoopServer.Current == null)
                 {
                    Common.Log.Info("No server found.");
                 }
                 else
                 {
                    Common.Log.Info($"Server state: {CoopServer.Current.State}.");
                 }
             },
             false));
        }
        protected override void OnSubModuleUnloaded()
        {
            CoopServer.ShutDownServer();
            base.OnSubModuleUnloaded();
        }

        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);
            if (Input.DebugInput.IsControlDown() && Input.DebugInput.IsKeyDown(InputKey.Tilde))
            {
                Debug.Console.Toggle();
            }

            if(m_ClientModel != null)
            {
                m_ClientModel.Update(TimeSpan.FromMilliseconds(dt));
            }
        }

        private void initLogger()
        {
            // NoHarmony
            this.Logging = true;

            // our own logger
            Common.Log.s_OnLogEntry = (Common.Log.ELevel eLevel, string sMessage) =>
            {
                using (StreamWriter sw = new StreamWriter("Coop.txt", true))
                    sw.WriteLine(sMessage);
                if (eLevel == Common.Log.ELevel.Info)
                {
                    InformationManager.DisplayMessage(new InformationMessage(sMessage, Color.White));
                }
                if (eLevel == Common.Log.ELevel.Warning)
                {
                    InformationManager.DisplayMessage(new InformationMessage(sMessage, Color.FromUint(0xFF0000)));
                }
                else if (eLevel == Common.Log.ELevel.Error)
                {
                    InformationManager.DisplayMessage(new InformationMessage(sMessage, Color.FromUint(0xFFFF00)));
                }
            };
        }

        public override void OnGameInitializationFinished(TaleWorlds.Core.Game game)
        {
            base.OnGameInitializationFinished(game);
            m_ClientModel = game.GetGameModel<ClientModel>();
            CoopClient.Client.SetTarget(m_ClientModel);
        }

        private readonly UpdateableList m_Updateables;
        private ClientModel m_ClientModel;
    }
}
