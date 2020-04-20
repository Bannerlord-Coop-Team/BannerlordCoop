using Coop.Common;
using Coop.Multiplayer.Network;
using Coop.Network;

namespace Coop.Game
{
    public static class CoopServer
    {
        public static Server Current = null;
        private static NetManagerServer NetManager = null;

        public static bool TryStartServer()
        {
            if(Current == null)
            {
                Current = new Server();
                ServerConfiguration config = new ServerConfiguration();
                Current.Start(config);
                Log.Debug("Created server.");
            }

            if(NetManager == null)
            {
                NetManager = new NetManagerServer(Current, new WorldData());
                NetManager.StartListening();
                Log.Debug("Setup network connection for server.");
            }
            return true;
        }

        public static void ShutDownServer()
        {
            Current?.Stop();
            NetManager?.Stop();
            NetManager = null;            
            Current = null;
        }
	}
}
