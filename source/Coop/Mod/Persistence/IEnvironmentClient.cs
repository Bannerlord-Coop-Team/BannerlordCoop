using System;
using System.Collections.Generic;
using Coop.Mod.Persistence.Party;
using JetBrains.Annotations;
using Sync.Store;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Persistence
{
    /// <summary>
    ///     Provides an abstraction layer between the persistence and the game for clients.
    /// </summary>
    public interface IEnvironmentClient
    {
        /// <summary>
        ///     Set the movement data of the given party as an authoritative action.
        /// </summary>
        void SetAuthoritative(MobileParty party, MovementData data);
        /// <summary>
        ///     Set the current position of the given party as an authoritative action.
        /// </summary>
        /// <param name="mManagedParty"></param>
        /// <param name="mapPosition"></param>
        void SetAuthoritative(MobileParty mManagedParty, Vec2 mapPosition);
        
        /// <summary>
        ///     The master campaign time. On the host this equals to the local campaign time.
        ///     On remote clients this is the latest campaign time dictated by the host.
        /// </summary>
        CampaignTime AuthoritativeTime { get; set; }

        /// <summary>
        ///     Returns all parties that are the main parties of human players, local or remote.
        /// </summary>
        IEnumerable<MobileParty> PlayerMainParties { get; }
        
        /// <summary>
        ///     Gets the synchronization for <see cref="MobileParty"/> instances.
        /// </summary>
        MobilePartySync PartySync { get; }

        /// <summary>
        ///     Returns the object store shared with all other clients.
        /// </summary>
        [NotNull]
        RemoteStore Store { get; }

        /// <summary>
        ///     Sets whether a party is controlled by a human player (locally or remote). Called
        ///     by the persistence framework whenever the controller changes.
        /// </summary>
        /// <param name="guid">
        ///     Party guid, to be resolved using <see cref="GetMobilePartyById" />
        /// </param>
        /// <param name="isPlayerControlled"></param>
        void SetIsPlayerControlled(MBGUID guid, bool isPlayerControlled);

        /// <summary>
        ///     Returns a party given its guid.
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        [CanBeNull]
        MobileParty GetMobilePartyById(MBGUID guid);
        /// <summary>
        ///     Called when a party enter the scope of the local client.
        /// </summary>
        /// <param name="party"></param>
        /// <param name="movementData"></param>
        void ScopeEntered([NotNull] MobileParty party, MovementData movementData);
        /// <summary>
        ///     Called when a party leaves the scope of the local client.
        /// </summary>
        /// <param name="party"></param>
        void ScopeLeft([NotNull] MobileParty party);
    }
}
