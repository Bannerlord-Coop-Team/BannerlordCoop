using JetBrains.Annotations;
using RailgunNet.System.Types;
using TaleWorlds.CampaignSystem;

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
        /// <param name="newValue"></param>
        void RequestMovement([NotNull] MovementData newValue);
    }
}