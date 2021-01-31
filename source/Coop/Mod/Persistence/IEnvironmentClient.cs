using System.Collections.Generic;
using Coop.Mod.Persistence.Party;
using JetBrains.Annotations;
using Sync;
using Sync.Store;
using TaleWorlds.CampaignSystem;

namespace RemoteAction
{
    /// <summary>
    ///     Provides an abstraction layer between the persistence and the game for clients.
    /// </summary>
    public interface IEnvironmentClient
    {
        /// <summary>
        ///     Access to the movement data for all parties in the clients game world.
        /// </summary>
        FieldAccessGroup<MobileParty, MovementData> TargetPosition { get; }
        
        /// <summary>
        ///     The master campaign time. On the host this equals to the local campaign time.
        ///     On remote clients this is the latest campaign time dictated by the host.
        /// </summary>
        CampaignTime AuthoritativeTime { get; set; }

        /// <summary>
        ///     Returns all parties that are controlled by human players, local or remote.
        /// </summary>
        IEnumerable<MobileParty> PlayerControlledParties { get; }

        /// <summary>
        ///     Returns the object store shared with all other clients.
        /// </summary>
        [NotNull]
        RemoteStore Store { get; }

        /// <summary>
        ///     Sets whether a party is controlled by a human player (locally or remote). Called
        ///     by the persistence framework whenever the controller changes.
        /// </summary>
        /// <param name="iPartyIndex">
        ///     Party index, to be resolved using <see cref="GetMobilePartyByIndex" />
        /// </param>
        /// <param name="isPlayerControlled"></param>
        void SetIsPlayerControlled(int iPartyIndex, bool isPlayerControlled);

        /// <summary>
        ///     Returns a party given its party index.
        /// </summary>
        /// <param name="iPartyIndex"></param>
        /// <returns></returns>
        [CanBeNull]
        MobileParty GetMobilePartyByIndex(int iPartyIndex);

        /// <summary>
        ///     Returns the active campaign of the client.
        /// </summary>
        /// <returns></returns>
        Campaign GetCurrentCampaign();
    }
}
