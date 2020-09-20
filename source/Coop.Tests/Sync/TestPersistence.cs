using System;
using System.Collections.Generic;
using Coop.Mod.Persistence;
using Coop.Mod.Persistence.RPC;
using Coop.NetImpl.LiteNet;
using RailgunNet.Connection.Client;
using RailgunNet.Connection.Server;
using RailgunNet.Factory;
using Sync;
using Sync.Store;
using TaleWorlds.CampaignSystem;

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

        public RPCSyncHandlers SyncHandlers { get; } = new RPCSyncHandlers();

        public void AddClient(
            RailRegistry registryClient,
            RailNetPeerWrapper connectionClientSide,
            RailNetPeerWrapper connectionServerSide)
        {
            RailClient client = new RailClient(registryClient);
            Rooms.Add(client.StartRoom());
            Clients.Add(client);
            Server.AddClient(connectionServerSide, "");
            client.SetPeer(connectionClientSide);
        }

        public void UpdateClients()
        {
            foreach (RailClient client in Clients)
            {
                client.Update();
            }
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

        public FieldAccessGroup<MobileParty, MovementData> TargetPosition { get; }
        public FieldAccess<Campaign, CampaignTimeControlMode> TimeControlMode { get; }
        public FieldAccess<Campaign, bool> TimeControlModeLock { get; }

        public CampaignTime AuthoritativeTime { get; set; } = CampaignTime.Zero;

        public void SetIsPlayerControlled(int iPartyIndex, bool isPlayerControlled)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<MobileParty> PlayerControlledParties { get; }
        public RemoteStore Store { get; }

        public MobileParty GetMobilePartyByIndex(int iPartyIndex)
        {
            throw new NotImplementedException();
        }

        public Campaign GetCurrentCampaign()
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

        public MobileParty GetMobilePartyByIndex(int iPartyIndex)
        {
            throw new NotImplementedException();
        }

        public Campaign GetCurrentCampaign()
        {
            throw new NotImplementedException();
        }
    }
}
