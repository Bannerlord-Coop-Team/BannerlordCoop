using Coop.Mod.GameSync;
using Coop.Mod.GameSync.Party;
using Coop.Mod.Persistence.Party;
using JetBrains.Annotations;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace Coop.Mod.Scope
{
    /// <summary>
    ///     Helper functions to work with <see cref="MobileParty"/> in regards to the synchronization scoping.
    /// </summary>
    public static class MobilePartyScopeHelper
    {
        private class State
        {
            public State(bool isInScope)
            {
                InScope = isInScope;
            }

            public bool InScope = false;
        }
        private static ConditionalWeakTable<MobileParty, State> Lookup = new ConditionalWeakTable<MobileParty, State>();

        /// <summary>
        ///     To be called when a <see cref="MobileParty"/> enters the scope of this game instance.
        /// </summary>
        /// <param name="party"></param>
        /// <param name="position"></param>
        /// <param name="facingDirection"></param>
        /// <param name="currentMovementData"></param>
        public static void Enter([NotNull] MobileParty party,
            Vec2 position,
            Vec2? facingDirection)
        {
            if(Lookup.TryGetValue(party, out State state))
            {
                state.InScope = true;
            }
            else
            {
                Lookup.Add(party, new State(true));
            }

            party.IsActive = true;
            bool shouldBeVisble = party.CurrentSettlement == null; // Parties inside of settlements should stay invisible. Otherwise they just stand around the gate.
            if (shouldBeVisble) 
            {
                party.IsVisible = true;
            }
            party.Party.Visuals.SetMapIconAsDirty();
            MobilePartyManaged.AuthoritativePositionChange(party, position, facingDirection);
        }
        /// <summary>
        ///     To be called when a <see cref="MobileParty"/> leaves the scope of this game instance.
        /// </summary>
        /// <param name="party"></param>
        public static void Leave([NotNull] MobileParty party)
        {
            if (Lookup.TryGetValue(party, out State state))
            {
                state.InScope = false;
            }
            else
            {
                Lookup.Add(party, new State(false));
            }

            party.IsActive = false;
            party.IsVisible = false;
            party.Party.Visuals.SetMapIconAsDirty();
            party.SetMoveModeHold();
            party.DisableAi();
        }

        /// <summary>
        ///     Returns if the party is in the scope of the local coop client. A party that is out of scope
        ///     shall be ignored by the client, because its data is currently not being synced. The server
        ///     decideds which parties are in scope if which client.
        ///     
        ///     For example, a party that is outside the view radius of the client is considered out of scope.
        ///     But for optimization purposes, the party game entity is still kept, just not updated.
        /// </summary>
        /// <param name="party"></param>
        /// <returns></returns>
        public static bool IsInClientScope(this MobileParty party)
        {
            if(Coop.IsServer || Coop.IsLocalPlayerMainParty(party))
            {
                return true;
            }

            if (!Lookup.TryGetValue(party, out State state))
            {
                // Unknown party. Maybe spawned by the client? Regardless, it's not in scope unless the server says so.
                Leave(party);
                return false;
            }

            return state.InScope;
        }
    }
}