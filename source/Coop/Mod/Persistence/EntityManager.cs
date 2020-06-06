using System;
using System.Collections.Generic;
using System.Linq;
using Coop.Mod.Persistence.Party;
using Coop.Mod.Persistence.World;
using NLog;
using RailgunNet.Connection.Server;
using RailgunNet.Logic;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Persistence
{
    /// <summary>
    ///     Makes sure that each syncable game entity has a corresponding entity in the
    ///     persistence framework.
    /// </summary>
    public class EntityManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly Dictionary<MobileParty, RailEntityServer> m_Parties =
            new Dictionary<MobileParty, RailEntityServer>();

        public IReadOnlyCollection<RailEntityServer> Parties => m_Parties.Values;
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

        /// <summary>
        ///     Called for each player controlled entity when the controlling player leaves the game.
        ///     The entity control has already been revoked from the player. This callback is expected
        ///     to handle any necessary clean up in the game entity system.
        /// </summary>
        public event Action<RailServerPeer, RailEntityServer> OnPlayerControlledEntityOrphaned;

        private void InitRoom(RailServerRoom room)
        {
            if (Campaign.Current == null)
            {
                throw new Exception("Unable to initialize game entities: Unexpected state. No game loaded?");
            }
            // TODO: If the server runs in a separate thread we need to synchronize modifying state.
            room.AddNewEntity<WorldEntityServer>();

            // Parties
            foreach (MobileParty party in Campaign.Current.MobileParties)
            {
                MobilePartyEntityServer entity = room.AddNewEntity<MobilePartyEntityServer>(
                    e => e.State.PartyId = party.Party.Index);
                m_Parties.Add(party, entity);
            }

            CampaignEvents.OnPartyDisbandedEvent.AddNonSerializedListener(this, OnPartyRemoved);
            CampaignEvents.OnLordPartySpawnedEvent.AddNonSerializedListener(this, OnPartyAdded);

            // Settlements
        }

        private void OnPartyRemoved(MobileParty party)
        {
            if (!m_Parties.ContainsKey(party))
            {
                Logger.Warn(
                    "Inconsistent internal state: {party} was removed, but never added.",
                    party);
                return;
            }

            m_Room.MarkForRemoval(m_Parties[party]);
            m_Parties.Remove(party);
        }

        private void OnPartyAdded(MobileParty party)
        {
            if (m_Parties.ContainsKey(party))
            {
                Logger.Warn("Inconsistent internal state: {party} was already registered.", party);
                return;
            }

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
            }

            if (m_Parties[party].Controller == null)
            {
                // TODO: Currently only the hosting player gets to control the main party. In a future version, every player gets their own party.
                peer.GrantControl(m_Parties[party]);
                Logger.Info("{party} control granted to {peer}.", party, peer);
            }
        }

        private void OnClientRemoved(RailServerPeer peer)
        {
            foreach (RailEntityServer controlledEntity in m_Room
                                                          .Entities.Where(e => e.Controller == peer)
                                                          .Select(e => e as RailEntityServer))
            {
                peer.RevokeControl(controlledEntity);
                OnPlayerControlledEntityOrphaned?.Invoke(peer, controlledEntity);
            }
        }

        private MobileParty GetPlayerParty(RailServerPeer peer)
        {
            if (Coop.IsClient || Coop.IsServer)
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
