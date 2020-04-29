using Coop.Game.Patch;
using RailgunNet;

namespace Coop.Game.Persistence
{
    public static class RailGameConfigurator
    {
        public static void SetInstanceAs(Component eComponent)
        {
            TimeControl.IsRemoteControlled = eComponent == Component.Client;
        }
    }
}
