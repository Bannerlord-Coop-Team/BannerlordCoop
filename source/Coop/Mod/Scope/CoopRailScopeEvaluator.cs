using Coop.Mod.Persistence.Party;
using JetBrains.Annotations;
using RailgunNet;
using RailgunNet.Logic;
using RailgunNet.Logic.Scope;
using RailgunNet.Util;
using TaleWorlds.Library;

namespace Coop.Mod.Scope
{
    /// <summary>
    ///     Railgun scope implementation for clients.
    /// </summary>
    [OnlyIn(Component.Server)]
    public class CoopRailScopeEvaluator : RailScopeEvaluator
    {
        public delegate MobilePartyEntityServer PlayerEntityGetter();

        public delegate float ScopeRangeGetter(MobilePartyEntityServer entity);

        /// <summary>
        ///     Creates a new scope evaluator.
        /// </summary>
        /// <param name="bIsForArbiter">True if this scope is for the arbiter client instance.</param>
        /// <param name="getPlayerParty">Getter for the clients main party.</param>
        /// <param name="getRange">Getter for the range of the scope.</param>
        public CoopRailScopeEvaluator(
            bool bIsForArbiter, 
            [NotNull] PlayerEntityGetter getPlayerParty, 
            [NotNull] ScopeRangeGetter getRange)
        {
            m_GetPlayerParty = getPlayerParty;
            m_GetRange = getRange;
            m_isForArbiter = bIsForArbiter;
        }
        /// <summary>
        ///     Evaluates whether the given event is in the scope of this client.
        /// </summary>
        /// <param name="evnt"></param>
        /// <returns></returns>
        public override bool Evaluate(RailEvent evnt)
        {
            return true;
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
            priority = 0.0f;

            const int minUpdateTicks = 500;
            if (ticksSinceSend > minUpdateTicks)
            {
                return true;
            }
            
            if(entity is MobilePartyEntityServer partyToCheck)
            {
                if (m_isForArbiter)
                {
                    // The arbiter needs info about all player parties
                    return partyToCheck.State.IsPlayerControlled;
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
        private bool m_isForArbiter;
    }
}