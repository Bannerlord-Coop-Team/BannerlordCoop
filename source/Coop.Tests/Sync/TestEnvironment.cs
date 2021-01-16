using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Coop.Mod.Persistence;
using Coop.Mod.Persistence.RPC;
using Coop.NetImpl.LiteNet;
using JetBrains.Annotations;
using RailgunNet.Connection.Client;
using RailgunNet.Factory;
using RailgunNet.Logic;
using RPC;
using Sync;
using Sync.Store;

namespace Coop.Tests.Sync
{
    public class TestEnvironment
    {
        public TestEnvironment(int iNumberOfClients)
        {
            ConnectionsRaw = new TestConnectionsRaw(iNumberOfClients);
            Connections = new TestConnections(ConnectionsRaw, false);
            Stores = new TestStores(Connections);
        }

        public TestEnvironment(
            int iNumberOfClients,
            Func<IEnvironmentClient, RailRegistry> clientRegistryCreator,
            Func<IEnvironmentServer, RailRegistry> serverRegistryCreator)
        {
            ConnectionsRaw = new TestConnectionsRaw(iNumberOfClients);
            Connections = new TestConnections(ConnectionsRaw, true);
            Stores = new TestStores(Connections);

            // Railgun
            RailSynchronizedFactory.Detect(Assembly.GetAssembly(typeof(RailBitBufferExtensions)));
            TestEnvironmentServer serverEnvironment = new TestEnvironmentServer(StoreServer);
            EventQueue = serverEnvironment.EventQueue;
            RailRegistry registryServer = serverRegistryCreator(serverEnvironment);
            Persistence = new TestPersistence(registryServer);

            foreach (((RailNetPeerWrapper First, RailNetPeerWrapper Second) First, RemoteStore
                Second) it in RailPeerClient.Zip(RailPeerServer).Zip(StoresClient))
            {
                RailNetPeerWrapper client = it.First.First;
                RailNetPeerWrapper server = it.First.Second;
                RemoteStore store = it.Second;

                RailRegistry registryClient =
                    clientRegistryCreator(new TestEnvironmentClient(store));
                Persistence.AddClient(registryClient, client, server);
            }

            // Let railgun do its initialization routine
            for (int i = 0; i < 5; ++i)
            {
                Persistence.UpdateServer();
                ExecuteSendsServer();
                Persistence.UpdateClients();
                ExecuteSendsClients();
            }
        }

        public EventBroadcastingQueue EventQueue { get; }

        [NotNull] public TestConnectionsRaw ConnectionsRaw { get; private set; }
        [NotNull] public TestConnections Connections { get; private set; }

        [NotNull] public TestStores Stores { get; private set; }

        [CanBeNull] public TestPersistence Persistence { get; private set; }

        public List<RemoteStore> StoresClient => Stores.StoresClient;

        public SharedRemoteStore StoreServer => Stores.StoreServer;

        public List<RailNetPeerWrapper> RailPeerClient =>
            Connections.ConnectionsClient.Select(c => c.GameStatePersistence)
                       .Cast<RailNetPeerWrapper>()
                       .ToList();

        public List<RailNetPeerWrapper> RailPeerServer =>
            Connections.ConnectionsServer.Select(c => c.GameStatePersistence)
                       .Cast<RailNetPeerWrapper>()
                       .ToList();

        public void ExecuteSendsClients()
        {
            ConnectionsRaw.ExecuteSendsClients();
        }

        public void ExecuteSendsServer()
        {
            ConnectionsRaw.ExecuteSendsServer();
        }

        public void Destroy()
        {
            Persistence = null;
            Stores = null;
            Connections = null;
            ConnectionsRaw = null;
            MethodPatchFactory.UnpatchAll();
        }

        public IClientAccess GetClientAccess(int iClientId)
        {
            return new ClientAccess(StoresClient[iClientId], Persistence?.Rooms[iClientId]);
        }

        private class ClientAccess : IClientAccess
        {
            private readonly RailClientRoom m_Room;
            private readonly RemoteStore m_Store;

            public ClientAccess(RemoteStore store, RailClientRoom room)
            {
                m_Store = store;
                m_Room = room;
            }

            public RemoteStore GetStore()
            {
                return m_Store;
            }

            public RailClientRoom GetRoom()
            {
                return m_Room;
            }
        }
    }
}
