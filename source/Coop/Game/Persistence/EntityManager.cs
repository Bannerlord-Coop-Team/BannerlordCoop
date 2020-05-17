using System;
using System.Collections.Generic;
using Coop.Game.Persistence.Party;
using Coop.Game.Persistence.World;
using NLog;
using RailgunNet.Connection.Server;
using RailgunNet.Logic;
using TaleWorlds.CampaignSystem;

namespace Coop.Game.Persistence
{
    public class EntityManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly Dictionary<MobileParty, RailEntityServer> m_Parties =
            new Dictionary<MobileParty, RailEntityServer>();

        private readonly RailServerRoom m_Room;
        private readonly RailServer m_Server;
        private RailServerPeer m_Arbiter;

        public EntityManager(RailServer server)
        {
            m_Server = server ?? throw new ArgumentNullException(nameof(server));
            m_Room = m_Server.StartRoom();
            InitRoom(m_Room);

            // Setup callbacks
            m_Server.ClientAdded += OnClientAdded;
            m_Server.ClientRemoved += OnClientRemoved;
        }

        private void InitRoom(RailServerRoom room)
        {
            room.AddNewEntity<WorldEntityServer>();

            foreach (MobileParty party in Campaign.Current.MobileParties)
            {
                MobilePartyEntityServer entity = room.AddNewEntity<MobilePartyEntityServer>(
                    e => e.State.PartyId = party.Party.Index);
                m_Parties.Add(party, entity);
            }

            CampaignEvents.OnPartyDisbandedEvent.AddNonSerializedListener(this, OnPartyRemoved);
            CampaignEvents.OnLordPartySpawnedEvent.AddNonSerializedListener(this, OnPartyAdded);
        }

        private void OnPartyRemoved(MobileParty party)
        {
            // TODO:
            m_Parties.Remove(party);
        }

        private void OnPartyAdded(MobileParty party)
        {
            MobilePartyEntityServer entity =
                m_Room.AddNewEntity<MobilePartyEntityServer>(
                    e => e.State.PartyId = party.Party.Index);
            m_Parties.Add(party, entity);
        }

        private void OnClientAdded(RailServerPeer peer)
        {
            if (IsArbiter(peer))
            {
                m_Arbiter = peer;
            }

            MobileParty party = GetPlayerParty(peer);
            if (party == null || !m_Parties.ContainsKey(party))
            {
                Logger.Warn("Player party not found.");
                return;
            }

            peer.GrantControl(m_Parties[party]);
            Logger.Info($"{party} control granted to {peer}.");
        }

        private void OnClientRemoved(RailServerPeer peer)
        {
            // TODO: Remove control
        }

        private MobileParty GetPlayerParty(RailServerPeer peer)
        {
            if (Coop.IsClient && Coop.IsServer)
            {
                return MobileParty.MainParty;
            }

            return null;
        }

        private bool IsArbiter(RailServerPeer peer)
        {
            // TODO: Implement
            return true;
        }
    }
}
