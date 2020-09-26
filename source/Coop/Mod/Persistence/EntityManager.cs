using System;
using System.Collections.Generic;
using System.Linq;
using Coop.Mod.Persistence.Party;
using Coop.Mod.Persistence.World;
using NLog;
using RailgunNet.Connection.Server;
using RailgunNet.Logic;
using RailgunNet.System.Types;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Persistence
{
    /// <summary>
    ///     Makes sure that each game entity that requires synchronization has a corresponding
    ///     entity in the persistence framework.
    /// </summary>
    public class EntityManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly object m_Lock = new object();

        private readonly Dictionary<MobileParty, RailEntityServer> m_Parties =
            new Dictionary<MobileParty, RailEntityServer>();

        private readonly List<MobileParty> m_PartiesToAdd = new List<MobileParty>();
        private readonly RailServerRoom m_Room;
        private readonly RailServer m_Server;

        public EntityManager(RailServer server)
        {
            m_Server = server ?? throw new ArgumentNullException(nameof(server));
            m_Room = m_Server.StartRoom();
            InitRoom(m_Room);
            m_Room.PostRoomUpdate += AddPendingParties;

            // Setup callbacks
            m_Server.ClientAdded += OnClientAdded;
            m_Server.ClientRemoved += OnClientRemoved;
        }

        public Dictionary<RailServerPeer, MobileParty> PlayerControllerParties { get; } =
            new Dictionary<RailServerPeer, MobileParty>();

        public WorldEntityServer WorldEntityServer { get; private set; }
        public bool SuppressInconsistentStateWarnings { get; set; } = false;

        public IReadOnlyCollection<RailEntityServer> Parties => m_Parties.Values;

        private void AddPendingParties(Tick tick)
        {
            List<MobileParty> toBeAdded;
            lock (m_Lock)
            {
                toBeAdded = new List<MobileParty>(m_PartiesToAdd);
                m_PartiesToAdd.Clear();
            }

            foreach (MobileParty party in toBeAdded)
            {
                MobilePartyEntityServer entity =
                    m_Room.AddNewEntity<MobilePartyEntityServer>(
                        e => e.State.PartyId = party.Party.Index);
                Logger.Debug("Added new entity {}.", entity);

                lock (m_Lock)
                {
                    m_Parties.Add(party, entity);
                }
            }
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
                throw new Exception(
                    "Unable to initialize game entities: Unexpected state. No game loaded?");
            }

            WorldEntityServer = room.AddNewEntity<WorldEntityServer>();

            // Parties
            foreach (MobileParty party in Campaign.Current.MobileParties)
            {
                if (party.Party.Index == MobilePartyState.InvalidPartyId)
                {
                    throw new Exception("Invalid party id!");
                }

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
            RailEntityServer entityToRemove;
            lock (m_Lock)
            {
                if (!m_Parties.ContainsKey(party))
                {
                    if (!SuppressInconsistentStateWarnings)
                    {
                        Logger.Warn(
                            "Inconsistent internal state: {party} was removed, but never added.",
                            party);
                    }

                    return;
                }

                entityToRemove = m_Parties[party];
                m_Parties.Remove(party);
            }

            m_Room.MarkForRemoval(entityToRemove);
            Logger.Debug("Marked entity {} for removal.", entityToRemove, party);
        }

        private void OnPartyAdded(MobileParty party)
        {
            if (party.Party.Index == MobilePartyState.InvalidPartyId)
            {
                throw new Exception("Invalid party id!");
            }

            lock (m_Lock)
            {
                if (m_Parties.ContainsKey(party))
                {
                    if (!SuppressInconsistentStateWarnings)
                    {
                        Logger.Warn(
                            "Inconsistent internal state: {party} was already registered.",
                            party);
                    }

                    return;
                }

                m_PartiesToAdd.Add(party);
            }
        }

        public void AddParty(MobileParty party)
        {
            MobilePartyEntityServer entity =
                m_Room.AddNewEntity<MobilePartyEntityServer>(
                    e => e.State.PartyId = party.Party.Index);
            Logger.Debug("Added new entity {}.", entity);

            lock (m_Lock)
            {
                m_Parties.Add(party, entity);
            }
        }

        public void GrantPartyControl(MobileParty party, RailServerPeer peer)
        {
            peer.GrantControl(m_Parties[party]);
            PlayerControllerParties.Add(peer, party);
        }

        private void OnClientAdded(RailServerPeer peer)
        {
            if (IsArbiter(peer))
            {
            }

            MobileParty party = GetPlayerParty(peer);
            lock (m_Lock)
            {
                if (party == null || !m_Parties.ContainsKey(party))
                {
                    Logger.Warn("Player party not found.");
                }

                if (m_Parties[party].Controller == null)
                {
                    // TODO: Currently only the hosting player gets to control the main party. In a future version, every player gets their own party.
                    GrantPartyControl(party, peer);
                    Logger.Info("{party} control granted to {peer}.", party, peer);
                }
            }
        }

        private void OnClientRemoved(RailServerPeer peer)
        {
            lock (m_Lock)
            {
                foreach (RailEntityServer controlledEntity in m_Room
                                                              .Entities.Where(
                                                                  e => e.Controller == peer)
                                                              .Select(e => e as RailEntityServer))
                {
                    peer.RevokeControl(controlledEntity);
                    OnPlayerControlledEntityOrphaned?.Invoke(peer, controlledEntity);
                }
            }
        }

        private MobileParty GetPlayerParty(RailServerPeer peer)
        {
            if (Coop.IsClientConnected || Coop.IsServer)
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
