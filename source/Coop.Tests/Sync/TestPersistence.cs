using System;
using System.Collections.Generic;
using Coop.Mod.Persistence;
using Coop.Mod.Persistence.Party;
using Coop.Mod.Persistence.RemoteAction;
using Coop.NetImpl.LiteNet;
using RailgunNet.Connection.Client;
using RailgunNet.Connection.Server;
using RailgunNet.Factory;
using RemoteAction;
using Sync.Store;
using Sync.Value;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Coop.Tests.Sync
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

    public class TestEnvironmentClient : IEnvironmentClient
    {
        public TestEnvironmentClient(RemoteStore store)
        {
            Store = store;
        }

        public FieldAccess<Campaign, CampaignTimeControlMode> TimeControlMode { get; }
        public FieldAccess<Campaign, bool> TimeControlModeLock { get; }

        public void SetAuthoritative(MobileParty party, MovementData data)
        {
            // TODO
            throw new NotImplementedException();
        }

        public void SetAuthoritative(MobileParty mManagedParty, Vec2 mapPosition)
        {
            throw new NotImplementedException();
        }

        public CampaignTime AuthoritativeTime { get; set; } = CampaignTime.Zero;

        public IEnumerable<MobileParty> PlayerMainParties { get; }
        public MobilePartySync PartySync { get; }
        public RemoteStore Store { get; }
        public void SetIsPlayerControlled(MBGUID guid, bool isPlayerControlled)
        {
            throw new NotImplementedException();
        }

        public MobileParty GetMobilePartyById(MBGUID guid)
        {
            throw new NotImplementedException();
        }
    }

    public class TestEnvironmentServer : IEnvironmentServer
    {
        public TestEnvironmentServer(SharedRemoteStore store)
        {
            Store = store;
            EventQueue = new EventBroadcastingQueue(Store, TimeSpan.FromSeconds(5));
        }

        public FieldAccessGroup<MobileParty, MovementData> TargetPosition { get; }
        public bool CanChangeTimeControlMode { get; }
        public SharedRemoteStore Store { get; }
        public EventBroadcastingQueue EventQueue { get; }

        public MobileParty GetMobilePartyById(MBGUID guid)
        {
            throw new NotImplementedException();
        }

        public MobilePartySync PartySync { get; }
    }
}