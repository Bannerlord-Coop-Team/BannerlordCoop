using Coop.NetImpl.LiteNet;
using Network.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Coop.Tests.Mission.P2PUtils
{
    public class P2PGroup : IDisposable
    {
        public readonly List<LiteNetP2PClient> Clients = new List<LiteNetP2PClient>();
        public LiteNetListenerServer Server;

        public TimeSpan TimeBetweenPolls = TimeSpan.FromMilliseconds(10);

        public NetworkConfiguration Config = new NetworkConfiguration();

        private string m_ConnectionString;

        public P2PGroup(string connectionString)
        {
            m_ConnectionString = connectionString;
            Config.NATType = NATType.Internal;
        }

        public (LiteNetP2PClient, LiteNetP2PClient) Connect2Clients()
        {

            AddServer();
            LiteNetP2PClient client1 = AddClient();
            LiteNetP2PClient client2 = AddClient();

            UpdateForXTime(
                TimeSpan.FromSeconds(1), 
                (c) =>  { return c.ConnectedPeersCount > 0; });

            if(client1.ConnectedPeersCount == 0 ||
               client2.ConnectedPeersCount == 0)
            {
                throw new Exception("Unable to connect clients.");
            }

            return (client1, client2);
        }

        /// <summary>
        /// Adds and start client connection.
        /// NOTE: Polling still needs to be called after adding.
        /// </summary>
        /// <returns></returns>
        public LiteNetP2PClient AddClient()
        {
            LiteNetP2PClient newClient = new LiteNetP2PClient(Config);
            newClient.ConnectToP2PServer(m_ConnectionString);
            Clients.Add(newClient);
            return newClient;
        }

        public LiteNetListenerServer AddServer()
        {
            if(Server == null)
            {
                Server serverSM = new Server(global::Network.Infrastructure.Server.EType.Direct);
                Server = new LiteNetListenerServer(serverSM, Config);

                serverSM.Start(new ServerConfiguration());

                Server.NetManager.Start(Config.LanPort);
            }

            return Server;
        }

        /// <summary>
        /// Polls server and clients until given time is reached,
        /// or all clients meet break condition.
        /// </summary>
        /// <param name="amount">Amount of time to update for.</param>
        /// <param name="untilAll">Break condition that all clients need to meet.</param>
        public void UpdateForXTime(TimeSpan amount, Func<LiteNetP2PClient, bool> untilAll = null)
        {
            DateTime startTime = DateTime.Now;
            while (DateTime.Now - startTime < amount &&
                   !Clients.All(c => {
                       if (untilAll == null) return true;
                       return untilAll(c); 
                   }))
            {
                Server.NetManager.PollEvents();
                Server.NetManager.NatPunchModule.PollEvents();
                Clients.ForEach((client) => client.Update(TimeSpan.Zero));
                Thread.Sleep(TimeBetweenPolls);
            }
        }

        public void Dispose()
        {
            Server.NetManager.Stop();
            Clients.ForEach((c) => c.Stop());
        }
    }
}
