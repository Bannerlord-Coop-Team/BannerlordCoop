using System;
using System.Collections.Generic;
using System.Linq;
using Coop.Mod.Patch;
using Coop.Mod.Patch.MobilePartyPatches;
using Coop.Mod.Persistence;
using Coop.Mod.Persistence.Party;
using Coop.Mod.Scope;
using Sync.Store;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod
{
    internal class GameEnvironmentClient : IEnvironmentClient
    {
        public GameEnvironmentClient()
        {
            TimeSynchronization.GetAuthoritativeTime += () => AuthoritativeTime;
        }

        public HashSet<MobileParty> PlayerControlledMainParties { get; } =
            new HashSet<MobileParty>();

        public CampaignTime AuthoritativeTime { get; set; } = CampaignTime.Never;

        public void SetIsPlayerControlled(Guid guid, bool isPlayerControlled)
        {
            MobileParty party = GetMobilePartyById(guid);

            if(party == null)
            {
                return;
            }

            if (isPlayerControlled)
            {
                PlayerControlledMainParties.Add(party);
            }
            else
            {
                PlayerControlledMainParties.Remove(party);
            }
        }

        public IEnumerable<MobileParty> PlayerMainParties => PlayerControlledMainParties;
        public MobilePartySync PartySync { get; } = CampaignMapMovement.Sync;

        public RemoteStore Store =>
            CoopClient.Instance.SyncedObjectStore ??
            throw new InvalidOperationException("Client not initialized.");
        
        public void ScopeEntered(MobileParty party, Vec2 mapPosition, Vec2? facingDirection, MovementData movementData)
        {
            MobilePartyScopeHelper.Enter(party, mapPosition, facingDirection, movementData);
        }

        public void ScopeLeft(MobileParty party)
        {
            MobilePartyScopeHelper.Leave(party);
        }

        #region Game state access
        public MobileParty GetMobilePartyById(Guid guid)
        {
            return CoopObjectManager.GetObject<MobileParty>(guid);
        }
        public void SetAuthoritative(MobileParty party, MovementData data)
        {
            CampaignMapMovement.RemoteMovementChanged(party, data);
        }
        public void SetAuthoritative(MobileParty party, Vec2 position, Vec2? facingDirection)
        {
            CampaignMapMovement.RemoteMapPositionChanged(party, position, facingDirection);
        }
        #endregion
    }
}
