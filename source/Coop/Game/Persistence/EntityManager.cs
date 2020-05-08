using System;
using Coop.Common;
using Coop.Game.Persistence.Party;
using Coop.Game.Persistence.World;
using RailgunNet.Connection.Server;
using TaleWorlds.CampaignSystem;

namespace Coop.Game.Persistence
{
    public class EntityManager
    {
        private readonly EntityMapping m_Mapping;
        private readonly RailServerRoom m_Room;
        private readonly RailServer m_Server;

        public EntityManager(RailServer server, EntityMapping mapping)
        {
            if (server == null)
            {
                throw new ArgumentNullException(nameof(server));
            }

            if (mapping == null)
            {
                throw new ArgumentNullException(nameof(mapping));
            }

            m_Server = server;
            m_Room = m_Server.StartRoom();
            m_Mapping = mapping;
            InitRoom(m_Room);

            // Setup callbacks
            m_Server.ClientAdded += OnClientAdded;
            m_Server.ClientRemoved += OnClientRemoved;
        }

        private void InitRoom(RailServerRoom room)
        {
            room.AddNewEntity<WorldEntityServer>();
        }

        private void OnClientAdded(RailServerPeer peer)
        {
            MobileParty party = GetPlayerParty(peer);
            if (party == null)
            {
                Log.Warn("Player party not found.");
                return;
            }

            MobilePartyEntityServer playerParty =
                m_Room.AddNewEntity<MobilePartyEntityServer>(
                    e => e.State.PartyId = party.Party.Index);
            peer.GrantControl(playerParty);
            Log.Info($"{party} control granted to {peer}.");
        }

        private void OnClientRemoved(RailServerPeer peer)
        {
        }

        private MobileParty GetPlayerParty(RailServerPeer peer)
        {
            if (Coop.IsClient && Coop.IsServer)
            {
                return MobileParty.MainParty;
            }

            return null;
        }
    }
}
