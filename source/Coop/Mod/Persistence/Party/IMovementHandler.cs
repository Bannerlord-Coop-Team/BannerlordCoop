using JetBrains.Annotations;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Persistence.Party
{
    /// <summary>
    ///     Interface for a synchronized handler of <see cref="MobileParty"/> movement changes.
    /// </summary>
    public interface IMovementHandler
    {
        /// <summary>
        ///     Requests a change of the movement data of the managed party.
        /// </summary>
        /// <param name="newValue"></param>
        void RequestMovement([NotNull] MovementData newValue);
        /// <summary>
        ///     Returns the latest authoritative movement for the managed party.
        /// </summary>
        /// <returns></returns>
        MovementData GetLatest();
    }
}