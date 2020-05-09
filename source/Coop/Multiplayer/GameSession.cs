using System;
using Coop.Network;
using NLog;

namespace Coop.Multiplayer
{
    public class GameSession
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

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

            Logger.Debug("Connection to server created {connection}", Connection);
        }

        public void Disconnect(EDisconnectReason eReason)
        {
            if (Connection == null)
            {
                throw new InvalidStateException("Client is not connected. Cannot disconnect.");
            }

            Connection.Disconnect(eReason);
            Logger.Debug(
                "Disconnect from server {connection}. Reason: {reason}.",
                Connection,
                eReason);
            Connection = null;
        }
    }
}
