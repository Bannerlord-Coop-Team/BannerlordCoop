using System;
using System.Collections.Generic;
using Coop.Mod.Persistence;
using Coop.Mod.Persistence.RemoteAction;
using Coop.NetImpl.LiteNet;
using Moq;
using RailgunNet.Connection.Client;
using RailgunNet.Connection.Server;
using RailgunNet.Factory;
using Sync.Store;
using TaleWorlds.CampaignSystem;
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
        private readonly Dictionary<MBGUID, MobileParty> Parties;
        public TestEnvironmentClient(RemoteStore store, Dictionary<MBGUID, MobileParty> mobileParties)
        {
            Parties = mobileParties;
            Mock = new Mock<IEnvironmentClient>();
            Mock.Setup(env => env.Store).Returns(store);
            Mock.Setup(env => env.GetMobilePartyById(It.IsAny<MBGUID>())).Returns((MBGUID id) => Parties[id]);
        }
    }

    public class TestEnvironmentServer
    {
        public Mock<IEnvironmentServer> Mock { get; }
        public EventBroadcastingQueue EventQueue { get; }
        private readonly Dictionary<MBGUID, MobileParty> Parties;

        public TestEnvironmentServer(SharedRemoteStore store, Dictionary<MBGUID, MobileParty> mobileParties)
        {
            Parties = mobileParties;
            EventQueue = new EventBroadcastingQueue(store, TimeSpan.FromSeconds(5));
            Mock = new Mock<IEnvironmentServer>();
            Mock.Setup(env => env.Store).Returns(store);
            Mock.Setup(env => env.EventQueue).Returns(EventQueue);
            Mock.Setup(env => env.GetMobilePartyById(It.IsAny<MBGUID>())).Returns((MBGUID id) => Parties[id]);
        }
    }
}