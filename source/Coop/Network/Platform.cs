using Coop.Multiplayer.Steam;

namespace Coop.Network
{
    public static class Platform
    {
        public static INetwork Create()
        {
            return new NetworkSteam();
        }
    }
}
