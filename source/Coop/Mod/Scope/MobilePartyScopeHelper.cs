using Coop.Mod.Patch.MobilePartyPatches;
using Coop.Mod.Persistence.Party;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace Coop.Mod.Scope
{
    /// <summary>
    ///     Helper functions to work with <see cref="MobileParty"/> in regards to the synchronization scoping.
    /// </summary>
    public static class MobilePartyScopeHelper
    {
        /// <summary>
        ///     To be called when a <see cref="MobileParty"/> enters the scope of this game instance.
        /// </summary>
        /// <param name="party"></param>
        /// <param name="position"></param>
        /// <param name="facingDirection"></param>
        /// <param name="currentMovementData"></param>
        public static void Enter([NotNull] MobileParty party,
            Vec2 position,
            Vec2? facingDirection,
            MovementData currentMovementData)
        {
            // TODO: make sure the party visuals are correct
            CampaignMapMovement.RemoteMovementChanged(party, currentMovementData);
            CampaignMapMovement.RemoteMapPositionChanged(party, position, facingDirection);
            party.IsActive = true;
        }
        /// <summary>
        ///     To be called when a <see cref="MobileParty"/> leaves the scope of this game instance.
        /// </summary>
        /// <param name="party"></param>
        public static void Leave([NotNull] MobileParty party)
        {
            party.IsActive = false;
        }
    }
}