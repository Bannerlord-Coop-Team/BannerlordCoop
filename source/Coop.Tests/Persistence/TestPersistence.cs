using System;
using System.Collections.Generic;
using Common;
using Coop.Mod.Persistence;
using Coop.Mod.Persistence.Party;
using Coop.Mod.Persistence.RemoteAction;
using Coop.NetImpl.LiteNet;
using CoopFramework;
using Moq;
using RailgunNet.Connection.Client;
using RailgunNet.Connection.Server;
using RailgunNet.Factory;
using Sync.Store;
using Sync.Value;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Coop.Tests.Persistence
{
    public class TestPersistence
    {
        public TestPersistence(RailRegistry registryServer)
        {
            Server = new RailServer(registryServer);
            Server.StartRoom();
        }

        public RailServer Server { get; }
        public List<RailClient> Clients { get; } = new List<RailClient>();

        public List<RailClientRoom> Rooms { get; } = new List<RailClientRoom>();

        public void AddClient(
            RailRegistry registryClient,
            RailNetPeerWrapper connectionClientSide,
            RailNetPeerWrapper connectionServerSide)
        {
            var client = new RailClient(registryClient);
            Rooms.Add(client.StartRoom());
            Clients.Add(client);
            Server.AddClient(connectionServerSide, "");
            client.SetPeer(connectionClientSide);
        }

        public void UpdateClients()
        {
            foreach (var client in Clients) client.Update();
        }

        public void UpdateServer()
        {
            Server?.Update();
        }
    }

    public class TestEnvironmentClient
    {
        public Mock<IEnvironmentClient> Mock { get; }
        public TestEnvironmentClient(RemoteStoreClient store)
        {
            Mock = new Mock<IEnvironmentClient>();
            Mock.Setup(env => env.Store).Returns(store);
        }
    }

    public class TestEnvironmentServer
    {
        public Mock<IEnvironmentServer> Mock { get; }
        public EventBroadcastingQueue EventQueue { get; }

        public TestEnvironmentServer(RemoteStoreServer store)
        {
            EventQueue = new EventBroadcastingQueue(store, TimeSpan.FromSeconds(60));
            Mock = new Mock<IEnvironmentServer>();
            Mock.Setup(env => env.Store).Returns(store);
            Mock.Setup(env => env.EventQueue).Returns(EventQueue);
        }
    }
}