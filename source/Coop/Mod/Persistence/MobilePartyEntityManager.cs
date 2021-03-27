using System;
using System.Collections.Generic;
using System.Linq;
using Coop.Mod.Patch.MobilePartyPatches;
using Coop.Mod.Patch.Party;
using Coop.Mod.Persistence.Party;
using Coop.Mod.Persistence.World;
using Coop.Mod.Scope;
using NLog;
using RailgunNet;
using RailgunNet.Connection.Server;
using RailgunNet.Logic;
using RailgunNet.System.Types;
using RailgunNet.Util;
using Sync.Value;
using TaleWorlds.CampaignSystem;
using Vec2 = TaleWorlds.Library.Vec2;

namespace Coop.Mod.Persistence
{
    /// <summary>
    ///     Makes sure that each <see cref="MobileParty"/> that requires synchronization has a corresponding
    ///     entity in the persistence framework.
    ///
    ///     This manager is used server side. The entities will be synchronized to the clients through RailGun.
    /// </summary>
    [OnlyIn(Component.Server)]
    public class MobilePartyEntityManager
    {
        /// <summary>
        ///     Construct an instance that manages the entities in <paramref name="server"/>.
        /// </summary>
        /// <param name="server">Server to manage</param>
        public MobilePartyEntityManager(RailServer server)
        {
            m_Server = server ?? throw new ArgumentNullException(nameof(server));
            m_Room = m_Server.StartRoom();
            InitRoom(m_Room);
            m_Room.PostRoomUpdate += AddPendingParties;
            m_Server.ClientAdded += OnClientAdded;
            m_Server.ClientRemoved += OnClientRemoved;
        }

        /// <summary>
        ///    Returns if a given party is controlled by a connected player.
        /// </summary>
        public bool IsControlledByClient(MobileParty party)
        {
            lock (m_Lock)
            {
                return m_ClientControlledParties.ContainsValue(party);
            }
        }

        public WorldEntityServer WorldEntityServer { get; private set; }
        public bool SuppressInconsistentStateWarnings { get; set; } = false;

        /// <summary>
        ///     Returns a copy of all currently known <see cref="MobileParty"/> that have a corresponding entity.
        ///     Attention: May contains null entries!
        /// </summary>
        public IReadOnlyCollection<RailEntityServer> Parties
        {
            get
            {
                lock (m_Lock)
                {
                    return m_Parties.Values.ToList();
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

            room.ClientJoined += controller =>
            {
                if (controller.Scope == null)
                {
                    throw new Exception("Connected clients should always be scoped!");
                }
                controller.Scope.Evaluator = new CoopRailScopeEvaluator(
                    room.Clients.Count == 1,    // the first client is always the arbiter.
                    (() =>
                {
                    if (m_ClientControlledParties.TryGetValue((RailServerPeer) controller, out MobileParty party))
                    {
                        return m_Parties[party];
                    }
                    return null;
                }));
            };
            
            WorldEntityServer = room.AddNewEntity<WorldEntityServer>();

            // Parties
            foreach (MobileParty party in Campaign.Current.MobileParties)
            {
                MobilePartyEntityServer entity = room.AddNewEntity<MobilePartyEntityServer>(
                    e => e.State.PartyId = party.Id);
                m_Parties.Add(party, entity);
            }

            CampaignEvents.OnPartyDisbandedEvent.AddNonSerializedListener(this, OnPartyRemoved);
            CampaignEvents.OnPartyRemovedEvent.AddNonSerializedListener(this, OnPartyRemoved);
            
            CampaignEvents.MobilePartyCreated.AddNonSerializedListener(this, OnPartyAdded);
            CampaignEvents.OnLordPartySpawnedEvent.AddNonSerializedListener(this, OnPartyAdded);
            BanditsCampaignBehaviorPatch.OnBanditAdded += (sender, e) => OnPartyAdded(e);

            // Settlements
        }
        public void AddParty(MobileParty party)
        {
            MobilePartyEntityServer entity =
                m_Room.AddNewEntity<MobilePartyEntityServer>(
                    e => e.State.PartyId = party.Id);
            Logger.Debug("Added new entity {}.", entity);

            lock (m_Lock)
            {
                m_Parties.Add(party, entity);
            }
        }

        public void GrantPartyControl(MobileParty party, RailServerPeer peer)
        {
            MobilePartyEntityServer correspondingEntity = null;
            lock (m_Lock)
            {
                if (!m_Parties.TryGetValue(party, out correspondingEntity) || correspondingEntity == null)
                {
                    if (!m_PartiesToAdd.Contains(party))
                    {
                        Logger.Warn("Failed to grant control of {Party} to {Peer}: No corrensponding entity found for party", party, peer);
                        return;
                    }
                    
                    // The party is still waiting to be added to the room. So we need to put off the control transfer until then.
                    m_PendingGrantControl[party] = peer;
                    return;
                }
                m_ClientControlledParties.Add(peer, party);
            }
            peer.GrantControl(correspondingEntity);
            party.Ai.SetDoNotMakeNewDecisions(true);
            Logger.Info("{Party} control granted to {Peer}", party, peer.Identifier);
        }

        #region Private
        private void AddPendingParties(Tick tick)
        {
            foreach (var (party, controller) in GetPartiesToBeAdded())
            {
                lock (m_Lock)
                {
                    if (m_Parties.ContainsKey(party))
                        continue; // Happens because we hook into multiple events that might trigger twice for one instance
                    
                    m_Parties.Add(party, null); // Reserve to prevent duplicate entity creation
                }

                // Need to leave m_Lock, otherwise the entity creation might deadlock since it needs to makes game state queries in the main thread
                MobilePartyEntityServer entity =
                    m_Room.AddNewEntity<MobilePartyEntityServer>(
                        e =>
                        {
                            e.State.PartyId = party.Id;
                        });
                Logger.Debug("Added new entity {}.", entity);

                if (controller != null)
                {
                    controller.GrantControl(entity);
                    party.Ai.SetDoNotMakeNewDecisions(true);
                }

                lock (m_Lock)
                {
                    m_Parties[party] = entity;
                }
            }
        }

        private List<Tuple<MobileParty, RailServerPeer>>  GetPartiesToBeAdded()
        {
            List<Tuple<MobileParty, RailServerPeer>> toBeAdded = new List<Tuple<MobileParty, RailServerPeer>>();
            lock (m_Lock)
            {
                foreach (var party in m_PartiesToAdd)
                {
                    RailServerPeer controller = null;
                    if (m_PendingGrantControl.ContainsKey(party))
                    {
                        controller = m_PendingGrantControl[party];
                        m_PendingGrantControl.Remove(party);
                    }

                    toBeAdded.Add(new Tuple<MobileParty, RailServerPeer>(party, controller));
                }

                m_PartiesToAdd.Clear();

                foreach (var orphanedGrantControl in m_PendingGrantControl)
                {
                    Logger.Warn("Failed to grant control of {Party} to {Peer}: No corrensponding entity found for party",
                        orphanedGrantControl.Key, orphanedGrantControl.Value);
                }

                m_PendingGrantControl.Clear();
            }

            return toBeAdded;
        }

        private void OnPartyRemoved(PartyBase partyBase)
        {
            OnPartyRemoved(partyBase.MobileParty);
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
                            "Inconsistent internal state: {Party} was removed, but never added",
                            party);
                    }

                    return;
                }

                entityToRemove = m_Parties[party];
                m_Parties.Remove(party);
                m_PartiesToAdd.Remove(party);
            }

            m_Room.MarkForRemoval(entityToRemove);
            Logger.Debug("Marked entity {EntityToRemove} for removal", entityToRemove);
        }

        private void OnPartyAdded(MobileParty party)
        {
            if (party.Id == Coop.InvalidId)
            {
                throw new Exception($"Invalid party id in {party}");
            }

            lock (m_Lock)
            {
                if (m_Parties.ContainsKey(party))
                {
                    return;
                }

                m_PartiesToAdd.Add(party);
            }
        }
        private void OnClientAdded(RailServerPeer peer)
        {
            // Nothing to do. Player parties will be assigned later when everything is transferred.
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
        
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly object m_Lock = new object();

        private readonly Dictionary<MobileParty, MobilePartyEntityServer> m_Parties =
            new Dictionary<MobileParty, MobilePartyEntityServer>();
        private readonly Dictionary<RailServerPeer, MobileParty> m_ClientControlledParties =
            new Dictionary<RailServerPeer, MobileParty>();
        private readonly List<MobileParty> m_PartiesToAdd = new List<MobileParty>();
        private readonly Dictionary<MobileParty, RailServerPeer> m_PendingGrantControl = new Dictionary<MobileParty, RailServerPeer>();
        private readonly RailServerRoom m_Room;
        private readonly RailServer m_Server;
        
        #endregion

        /// <summary>
        ///     Updates the positions of all managed parties.
        /// </summary>
        /// <param name="buffer"></param>
        public void UpdatePosition(List<Tuple<MobileParty, Vec2>> buffer)
        {
            foreach (var change in buffer)
            {
                if (m_Parties.TryGetValue(change.Item1, out MobilePartyEntityServer entity) && 
                    entity != null)
                {
                    entity.State.MapPosition = change.Item2;
                }
            }
        }
    }
}
