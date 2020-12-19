using ModTestingFramework;
using Network.Infrastructure;
using TaleWorlds.Engine;

namespace Coop.Mod
{
    class TestingCommands : TestCommands
    {
        public static void StartCoop()
        {
            TaleWorlds.MountAndBlade.Module.CurrentModule.ExecuteInitialStateOptionWithId("CoOp Campaign");
        }
    }
}
