using JetBrains.Annotations;
using RailgunNet.System.Types;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace Coop.Mod.Persistence.Party
{
    /// <summary>
    ///     Interface for a synchronized handler of <see cref="MobileParty"/> movement changes.
    /// </summary>
    public interface IMovementHandler
    {
        Tick Tick { get; }

        /// <summary>
        ///     Requests a change of the movement data of the managed party.
        /// </summary>
        /// <param name="currentPosition"></param>
        /// <param name="newValue"></param>
        void RequestMovement(Vec2 currentPosition, [NotNull] MovementData newValue);
    }
}