using System;
using Coop.Common;
using Coop.Network;

namespace Coop.Multiplayer
{
    public class GameSession
    {
        public GameSession(ISaveData worldData)
        {
            World = worldData;
        }

        public ISaveData World { get; }
        public ConnectionClient Connection { get; private set; }
        public event Action<ConnectionClient> OnConnectionCreated;

        public void ConnectionCreated(ConnectionClient connection)
        {
            if (Connection != null)
            {
                Disconnect(EDisconnectReason.ClientJoinedAnotherServer);
            }

            Connection = connection;
            OnConnectionCreated?.Invoke(Connection);
            Connection.Connect();

            Log.Debug($"Connection to server created {Connection}.");
        }

        public void Disconnect(EDisconnectReason eReason)
        {
            if (Connection == null)
            {
                throw new InvalidStateException("Client is not connected. Cannot disconnect.");
            }

            Connection.Disconnect(eReason);
            Log.Debug($"Disconnect from server {Connection}. Reason: {eReason}.");
            Connection = null;
        }
    }
}
