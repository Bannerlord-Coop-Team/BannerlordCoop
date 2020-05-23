using Coop.Multiplayer.Steam;
using Network;

namespace Network.Infrastructure
{
    public static class Platform
    {
        public static INetwork Create()
        {
            return new NetworkSteam();
        }
    }
}
