using Coop.Mod.Persistence.Party;
using Coop.Mod.Persistence.RemoteAction;
using JetBrains.Annotations;
using RailgunNet;
using RailgunNet.Logic;
using RailgunNet.Logic.Scope;
using RailgunNet.Util;
using System.Linq;
using TaleWorlds.Library;

namespace Coop.Mod.Scope
{
    /// <summary>
    ///     Railgun scope evaluator for clients. It is evaluated on the server to decide if an entity
    ///     or event should be synced to client.
    ///     
    ///     ATTENTION: Since the scope is being evaluated on the server, special care is needed when
    ///     implementing this scope evaluator. For example, you may not use <see cref="Coop.IsServer"/>
    ///     because it will always be true.
    ///     
    ///     Entities:   If an entity is in scope of a client, the server will send regular updates of
    ///                 the entities state to that client. If an entity goes out of scope of a client,
    ///                 it will be "frozen". A frozen entity does not receive any state updates. When
    ///                 a currently frozen entity enters the scope again, it will be unfrozen and
    ///                 receive state updates again.
    ///                 
    ///                 The following entity types have a defined scope:
    ///                 - MobilePartyEntity: Based on distance from the clients main party. On client 
    ///                                      side <see cref="MobilePartyScopeHelper"/> for handlers 
    ///                                      on scope changes.
    /// 
    ///     Events:     By default, events are broadcast to all clients. Special handling exists for:
    ///                 - EventActionBase: Used for RPC, those events can be associated with one or
    ///                                    more entities. If at least one of the given entities is
    ///                                    in scope, then the event is in scope as well.
    /// </summary>
    [OnlyIn(Component.Server)]
    public class CoopRailScopeEvaluator : RailScopeEvaluator
    {
        public delegate MobilePartyEntityServer PlayerEntityGetter();

        public delegate float ScopeRangeGetter(MobilePartyEntityServer entity);

        /// <summary>
        ///     Creates a new scope evaluator.
        /// </summary>
        /// <param name="isForClientOnServer">True if this scope is for the client running on the servers game instance.</param>
        /// <param name="getPlayerParty">Getter for the clients main party.</param>
        /// <param name="getRange">Getter for the range of the scope.</param>
        public CoopRailScopeEvaluator(
            bool isForClientOnServer, 
            [NotNull] PlayerEntityGetter getPlayerParty, 
            [NotNull] ScopeRangeGetter getRange)
        {
            m_GetPlayerParty = getPlayerParty;
            m_GetRange = getRange;
            m_IsForClientOnServer = isForClientOnServer;
        }
        /// <summary>
        ///     Evaluates whether the given event is in the scope of this client.
        /// </summary>
        /// <param name="evnt"></param>
        /// <returns></returns>
        public override bool Evaluate(RailEvent evnt)
        {
            switch(evnt)
            {
                case EventActionBase ev:
                    if(ev.AffectedEntities == null)
                    {
                        // Event is not restricted
                        return true;
                    }

                    return ev.AffectedEntities.Select(id =>
                    {
                        if(CoopServer.Instance.Persistence.Room.TryGet(id, out RailEntityBase entity))
                        {
                            return entity;
                        }
                        return null;
                    }).Any(e => e != null && evaluate(e, out float f));
                default:
                    return true;
            }
        }

        /// <summary>
        ///     Evaluates whether the given entity is in the scope of this client.
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="ticksSinceSend"></param>
        /// <param name="ticksSinceAck"></param>
        /// <param name="priority"></param>
        /// <returns></returns>
        public override bool Evaluate(
            RailEntityBase entity,
            int ticksSinceSend,
            int ticksSinceAck,
            out float priority)
        {
            return evaluate(entity, out priority);
        }
        private bool evaluate(RailEntityBase entity, out float priority)
        {
            priority = 0.0f;

            if (entity is MobilePartyEntityServer partyToCheck)
            {
                if (m_IsForClientOnServer)
                {
                    // The client running on the server does not need any updates for parties.
                    return false;
                }

                // For parties we check if it is within range
                MobilePartyEntityServer playerParty = m_GetPlayerParty.Invoke();
                if (playerParty == null)
                {
                    return false;
                }

                if (playerParty == partyToCheck)
                {
                    // Always relevant
                    return true;
                }

                // compute distance from players party
                Vec2 playerPos = playerParty.State.MapPosition.Vec2;
                Vec2 otherPos = partyToCheck.State.MapPosition.Vec2;
                float fDistSqr = playerPos.DistanceSquared(otherPos);

                // set priority accordingly
                priority = fDistSqr;
                float fRange = m_GetRange(playerParty);
                bool bShouldBeSynced = fDistSqr <= fRange * fRange;
                return bShouldBeSynced;
            }

            // The other entities are world state, so they are always relevant
            return true;
        }

        private PlayerEntityGetter m_GetPlayerParty;
        private ScopeRangeGetter m_GetRange;
        private bool m_IsForClientOnServer;
    }
}