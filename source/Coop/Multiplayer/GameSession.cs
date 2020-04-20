using Coop.Common;
using Coop.Network;

namespace Coop.Multiplayer
{
    public class GameSession
    {
        public IWorldData World { get; private set; } 
        public ConnectionClient Connection { get; private set; }
        public GameSession(IWorldData worldData)
        {
            this.World = worldData;
        }

        public void OnConnectionCreated(ConnectionClient connection)
        {
            if (Connection != null)
            {
                throw new InvalidStateException($"Client already connected to {Connection}. Cannot create a second connection to {connection}.");
            }
            Connection = connection;
            Connection.Connect();
            
            Log.Debug($"Connection to server created {Connection.ToString()}.");
        }

        public void OnDisconnect(EDisconnectReason eReason)
        {
            if (Connection == null)
            {
                throw new InvalidStateException($"Client is not connected. Cannot disconnect.");
            }
            Connection.Disconnect(eReason);
            Log.Debug($"Disconnect from server {Connection}. Reason: {eReason}.");
            Connection = null;
        }
    }
}
