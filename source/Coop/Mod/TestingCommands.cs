using ModTestingFramework;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Engine;

namespace Coop.Mod
{
    class TestingCommands : TestCommands
    {
        public static void StartCoop()
        {
            Module.CurrentModule.ExecuteInitialStateOptionWithId(Main.CoopCampaign.Id);
        }
    }
}
