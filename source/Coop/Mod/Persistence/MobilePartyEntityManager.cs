using System;
using System.Collections.Generic;
using System.Linq;
using Common;
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
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
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

        /// <summary>
        ///     Returns a copy of all currently known <see cref="RailEntityServer"/>.
        ///     Attention: May contains null entries!
        /// </summary>
        public IReadOnlyCollection<RailEntityServer> ServerPartyEntities
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
        ///     Returns a copy of all parties currently controlled by a player.
        /// </summary>
        public IReadOnlyCollection<MobileParty> PlayerControlledParties
        {
            get
            {
                lock(m_Lock)
                {
                    return m_ClientControlledParties.Values.ToList();
                }
            }
        }

        /// <summary>
        ///     Returns the corresponding entity for a mobile party, if it exists.
        /// </summary>
        public bool TryGetEntity(MobileParty p, out MobilePartyEntityServer e)
        {
            return m_Parties.TryGetValue(p, out e);
        }

        /// <summary>
        ///     Called when a mobile party entity enters the scope of the client, immediately before 
        ///     sending the current state. The state of the entity may be changed in this callback.
        /// </summary>
        public event Action<RailController, MobilePartyEntityServer> OnBeforePartyScopeEnter;

        /// <summary>
        ///     Called when a mobile party entity leaves the scope of the client, immediately before
        ///     sending the freeze to the client. Changes to state of the entity in this callback
        ///     will not be synced to the client until the entity enters the scope again.
        /// </summary>
        public event Action<RailController, MobilePartyEntityServer> OnBeforePartyScopeLeave;

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
                    room.Clients.Count == 1,    // the first client is always the one running on the server game instance.
                    () =>
                {
                    if (m_ClientControlledParties.TryGetValue((RailServerPeer) controller, out MobileParty party))
                    {
                        return m_Parties.ContainsKey(party) ? m_Parties[party] : null;
                    }
                    return null;
                },
                GetScopeRange);
                controller.Scope.OnBeforeScopeEnter += BeforeScopeEnter;
                controller.Scope.OnBeforeScopeLeave += BeforeScopeLeave;
            };
            room.ClientLeft += controller =>
            {
                controller.Scope.OnBeforeScopeEnter -= BeforeScopeEnter;
                controller.Scope.OnBeforeScopeLeave -= BeforeScopeLeave;
            };
            WorldEntityServer = room.AddNewEntity<WorldEntityServer>();
        }

        public float ClientScopeRangeFactor = 1f;
        private float GetScopeRange(MobilePartyEntityServer entity)
        {
            if(entity.Instance != null)
            {
                return entity.Instance.SeeingRange * ClientScopeRangeFactor;
            }
            return 0f;
        }
        public void AddParty(TaleWorlds.CampaignSystem.Party.MobileParty party)
        {
            lock (m_Lock)
            {
                if (m_Parties.ContainsKey(party))
                {
                    return;
                }

                m_PartiesToAdd.Add(party);
            }
        }

        public void RemoveParty(MobileParty party)
        {
            RailEntityServer entityToRemove;
            lock (m_Lock)
            {
                if (!m_Parties.ContainsKey(party))
                {
                    return;
                }

                entityToRemove = m_Parties[party];
                m_Parties.Remove(party);
                m_PartiesToAdd.Remove(party);
            }

            m_Room.MarkForRemoval(entityToRemove);
            Logger.Debug("Marked entity {EntityToRemove} for removal", entityToRemove);
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
                
            }
            grantControl(correspondingEntity, party, peer);
        }

        #region Private
        private void grantControl(MobilePartyEntityServer entity, MobileParty party, RailServerPeer peer)
        {
            lock (m_Lock)
            {
                if (m_ClientControlledParties.TryGetValue(peer, out MobileParty controlledParty))
                {
                    RailEntityServer controlledEntity = peer.ControlledEntities.FirstOrDefault() as RailEntityServer;
                    Logger.Warn($"Client {peer} may only control 1 party at a time. Revoking control over {controlledParty}.");
                    peer.RevokeControl(controlledEntity);
                    m_ClientControlledParties[peer] = party;
                }
                else
                {
                    m_ClientControlledParties.Add(peer, party);
                }
            }
            peer.GrantControl(entity);
            party.Ai.SetDoNotMakeNewDecisions(true);
            Logger.Info("{Party} control granted to {Peer}", party, peer.Identifier);
        }

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

                Guid partyGuid = CoopObjectManager.GetGuid(party);

                // Need to leave m_Lock, otherwise the entity creation might deadlock since it needs to makes game state queries in the main thread
                MobilePartyEntityServer entity =
                    m_Room.AddNewEntity<MobilePartyEntityServer>(
                        e =>
                        {
                            e.State.PartyId = partyGuid;
                        });
                Logger.Debug("Added new entity {}.", entity);

                if (controller != null)
                {
                    grantControl(entity, party, controller);
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

        private void OnPartyAdded(MobileParty party)
        {
            if (CoopObjectManager.GetGuid(party) == Coop.InvalidId)
            {
                Logger.Warn($"{party} not present in CoopObjectManager. Somehow bypassed CoopManager. Will not be synced.");
                return;
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
                                                              .Entities.Values.Where(
                                                                  e => e.Controller == peer)
                                                              .Select(e => e as RailEntityServer))
                {
                    peer.RevokeControl(controlledEntity);
                    OnPlayerControlledEntityOrphaned?.Invoke(peer, controlledEntity);
                }
            }
        }
        private Dictionary<RailController, Dictionary<MobilePartyEntityServer, bool>> m_RemoteIsFrozen = new Dictionary<RailController, Dictionary<MobilePartyEntityServer, bool>> { };
        private void BeforeScopeEnter(RailController controller, RailEntityServer entity)
        {
            if (entity is MobilePartyEntityServer partyEntity)
            {
                if (!m_RemoteIsFrozen.TryGetValue(controller, out Dictionary<MobilePartyEntityServer, bool> frozenLookup))
                {
                    m_RemoteIsFrozen.Add(controller, new Dictionary<MobilePartyEntityServer, bool> { });
                    frozenLookup = m_RemoteIsFrozen[controller];
                }

                if(!frozenLookup.TryGetValue(partyEntity, out bool wasFrozen) || wasFrozen)
                {
                    OnBeforePartyScopeEnter?.Invoke(controller, partyEntity);
                    frozenLookup[partyEntity] = false;
                }
            }
        }
        private void BeforeScopeLeave(RailController controller, RailEntityServer entity)
        {
            if (entity is MobilePartyEntityServer partyEntity)
            {
                if (!m_RemoteIsFrozen.TryGetValue(controller, out Dictionary<MobilePartyEntityServer, bool> frozenLookup))
                {
                    m_RemoteIsFrozen.Add(controller, new Dictionary<MobilePartyEntityServer, bool> { });
                    frozenLookup = m_RemoteIsFrozen[controller];
                }

                if (!frozenLookup.TryGetValue(partyEntity, out bool wasFrozen) || !wasFrozen)
                {
                    OnBeforePartyScopeLeave?.Invoke(controller, partyEntity);
                    frozenLookup[partyEntity] = true;
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
