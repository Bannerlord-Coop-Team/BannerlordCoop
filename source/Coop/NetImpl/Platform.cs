using Coop.NetImpl.Steam;
using Network.Infrastructure;

namespace Coop.NetImpl
{
    public static class Platform
    {
        public static INetwork Create()
        {
            return new NetworkSteam();
        }
    }
}
