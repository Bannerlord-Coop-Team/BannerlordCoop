using Coop.Common;
using Coop.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Multiplayer
{
    public class ClientSession
    {
        public ConnectionClient Connection { get; private set; }
        public ClientSession()
        {
        }

        public void OnConnectionCreated(ConnectionClient connection)
        {
            if (Connection != null)
            {
                throw new InvalidStateException($"Client already connected to {Connection}. Cannot create a second connection to {connection}.");
            }
            Connection = connection;
            Connection.Connect();
            
            Log.Info($"Client connection created {Connection}.");
        }

        public void OnDisconnect(EDisconnectReason eReason)
        {
            if (Connection == null)
            {
                throw new InvalidStateException($"Client is not connected. Cannot disconnect.");
            }
            Connection.Disconnect(eReason);
            Log.Info($"Disconnect from server {Connection}. Reason: {eReason}.");
            Connection = null;
        }
    }
}
