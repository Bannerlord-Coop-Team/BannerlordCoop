using System;
using System.Collections.Generic;
using System.Linq;
using Coop.Mod.Patch;
using Coop.Mod.Persistence;
using Coop.Mod.Persistence.Party;
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

        public FieldAccessGroup<MobileParty, MovementData> TargetPosition =>
            CampaignMapMovement.Movement;

        public FieldAccess<Campaign, CampaignTimeControlMode> TimeControlMode =>
            TimeControl.TimeControlMode;

        public FieldAccess<Campaign, bool> TimeControlModeLock => TimeControl.TimeControlModeLock;

        public CampaignTime AuthoritativeTime { get; set; } = CampaignTime.Never;

        public void SetIsPlayerControlled(int iPartyIndex, bool isPlayerControlled)
        {
            if (isPlayerControlled)
            {
                PlayerControlledMobileParties.Add(GetMobilePartyByIndex(iPartyIndex));
            }
            else
            {
                PlayerControlledMobileParties.Remove(GetMobilePartyByIndex(iPartyIndex));
            }
        }

        public IEnumerable<MobileParty> PlayerControlledParties => PlayerControlledMobileParties;

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
