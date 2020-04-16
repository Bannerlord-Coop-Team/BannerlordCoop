using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace Coop.Multiplayer
{
    class Main : MBSubModuleBase
    {
        private Coop.Network.INetwork m_Platform;

        protected override void OnSubModuleLoad()
        {
            var harmony = new Harmony("com.TaleWorlds.MountAndBlade.Bannerlord");
            harmony.PatchAll();

            m_Platform = Coop.Network.Platform.Create();
            if(!m_Platform.Connect())
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
    }
}
