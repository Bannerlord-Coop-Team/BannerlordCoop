using System;
using System.Collections.Generic;
using System.Linq;
using Coop.Mod.Patch;
using Coop.Mod.Patch.MobilePartyPatches;
using Coop.Mod.Persistence;
using Coop.Mod.Persistence.Party;
using RemoteAction;
using Sync;
using Sync.Store;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod
{
    internal class GameEnvironmentClient : IEnvironmentClient
    {
        public GameEnvironmentClient()
        {
            TimeSynchronization.GetAuthoritativeTime += () => AuthoritativeTime;
        }

        public HashSet<MobileParty> PlayerControlledMobileParties { get; } =
            new HashSet<MobileParty>();

        public void SetAuthoritative(MobileParty party, MovementData data)
        {
            // TODO
            throw new NotImplementedException();
        }

        public CampaignTime AuthoritativeTime { get; set; } = CampaignTime.Never;

        public void SetIsPlayerControlled(int iPartyIndex, bool isPlayerControlled)
        {
            MobileParty party = GetMobilePartyByIndex(iPartyIndex);

            if(party == null)
            {
                return;
            }

            if (isPlayerControlled)
            {
                PlayerControlledMobileParties.Add(party);
            }
            else
            {
                PlayerControlledMobileParties.Remove(party);
            }
        }

        public IEnumerable<MobileParty> PlayerControlledParties => PlayerControlledMobileParties;
        public MobilePartySync PartySync { get; } = CampaignMapMovement.Sync;

        public RemoteStore Store =>
            CoopClient.Instance.SyncedObjectStore ??
            throw new InvalidOperationException("Client not initialized.");

        #region Game state access
        public MobileParty GetMobilePartyByIndex(int iPartyIndex)
        {
            return MobileParty.All.SingleOrDefault(p => p.Party.Index == iPartyIndex);
        }

        public Campaign GetCurrentCampaign()
        {
            return Campaign.Current;
        }
        #endregion
    }
}
