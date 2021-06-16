using ModTestingFramework;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Engine;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod
{
    class TestingCommands : TestCommands
    {
        public static void StartCoop()
        {
            Module.CurrentModule.ExecuteInitialStateOptionWithId(Main.CoopCampaign.Id);
        }

        public static void ExitToMainMenu()
        {
            MBGameManager.EndGame();
        }

        public static void SaveGame(string[] saveName)
        {
            Campaign.Current.SaveHandler.SaveAs(saveName[0]);
        }

        public static void LoadGame(string[] saveName)
        {
            CoopServer.Instance.StartGame(saveName[0]);
        }
    }
}
