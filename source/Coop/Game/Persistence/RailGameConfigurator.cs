using RailgunNet;

namespace Coop.Game.Persistence
{
    public static class RailGameConfigurator
    {
        public static void SetInstanceAs(Component eComponent)
        {
            Patch.TimeControl.IsRemoteControlled = eComponent == Component.Client;
        }
    }
}
