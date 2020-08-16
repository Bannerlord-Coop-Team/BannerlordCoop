using System;
using System.Linq;
using Coop.Mod.Patch;
using Coop.Mod.Persistence;
using Sync;
using Sync.Store;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod
{
    internal class GameEnvironmentClient : IEnvironmentClient
    {
        public FieldAccessGroup<MobileParty, MovementData> TargetPosition =>
            CampaignMapMovement.Movement;

        public FieldAccess<Campaign, CampaignTimeControlMode> TimeControlMode =>
            TimeControl.TimeControlMode;

        public FieldAccess<Campaign, bool> TimeControlModeLock => TimeControl.TimeControlModeLock;

        public GameEnvironmentClient()
        {
            Patch.TimeSynchronization.GetAuthoritativeTime += () => AuthoritativeTime;
        }

        public CampaignTime AuthoritativeTime {
            get;
            set;
        } = CampaignTime.Zero;

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
