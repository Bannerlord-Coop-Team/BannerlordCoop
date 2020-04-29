using Coop.Common;
using Coop.Multiplayer.Network;
using Coop.Network;
using System;

namespace Coop.Multiplayer
{
    public class GameSession
    {
        public ISaveData World { get; private set; } 
        public ConnectionClient Connection { get; private set; }
        public event Action<ConnectionClient> OnConnectionCreated;
        public GameSession(ISaveData worldData)
        {
            this.World = worldData;
        }

        public void ConnectionCreated(ConnectionClient connection)
        {
            if (Connection != null)
            {
                throw new InvalidStateException($"Client already connected to {Connection}. Cannot create a second connection to {connection}.");
            }
            Connection = connection;
            OnConnectionCreated?.Invoke(Connection);
            Connection.Connect();
            
            Log.Debug($"Connection to server created {Connection.ToString()}.");
        }

        public void Disconnect(EDisconnectReason eReason)
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
