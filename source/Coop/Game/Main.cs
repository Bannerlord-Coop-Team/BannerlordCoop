using Coop.Game.Debug;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace Coop.Game
{
    class Main : MBSubModuleBase
    {
        private Network.INetwork m_Platform;

        protected override void OnSubModuleLoad()
        {
            var harmony = new Harmony("com.TaleWorlds.MountAndBlade.Bannerlord");
            harmony.PatchAll();

            m_Platform = Network.Platform.Create();
            if (!m_Platform.Connect())
            {
                InformationManager.AddSystemNotification("Failed to connect to steam.");
            }

            Module.CurrentModule.AddInitialStateOption(new InitialStateOption("Clicky",
             new TextObject("Clicky!", null),
             9990,
             () =>
             {
                 string sMessage = m_Platform.IsConnected ? "Connected" : "Not connected";
                 InformationManager.DisplayMessage(new InformationMessage(sMessage));
             },
             false));
        }
        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);
            if (Input.DebugInput.IsControlDown() && Input.DebugInput.IsKeyDown(InputKey.Tilde))
            {
                Console.Toggle();
            }
        }
    }
}
