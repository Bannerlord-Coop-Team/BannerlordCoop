using System;
using Coop.Game.Persistence.Party;
using Coop.Game.Persistence.World;
using NLog;
using RailgunNet.Connection.Server;
using TaleWorlds.CampaignSystem;

namespace Coop.Game.Persistence
{
    public class EntityManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly RailServerRoom m_Room;
        private readonly RailServer m_Server;

        public EntityManager(RailServer server)
        {
            if (server == null)
            {
                throw new ArgumentNullException(nameof(server));
            }

            m_Server = server;
            m_Room = m_Server.StartRoom();
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
                Logger.Warn("Player party not found.");
                return;
            }

            MobilePartyEntityServer playerParty =
                m_Room.AddNewEntity<MobilePartyEntityServer>(
                    e => e.State.PartyId = party.Party.Index);
            peer.GrantControl(playerParty);
            Logger.Info($"{party} control granted to {peer}.");
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
