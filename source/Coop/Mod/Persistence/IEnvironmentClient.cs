using System.Collections.Generic;
using JetBrains.Annotations;
using Sync;
using Sync.Store;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Persistence
{
    public interface IEnvironmentClient
    {
        FieldAccessGroup<MobileParty, MovementData> TargetPosition { get; }
        FieldAccess<Campaign, CampaignTimeControlMode> TimeControlMode { get; }
        FieldAccess<Campaign, bool> TimeControlModeLock { get; }
        CampaignTime AuthoritativeTime { get; set; }
        IEnumerable<MobileParty> PlayerControlledParties { get; }

        [NotNull] RemoteStore Store { get; }
        void SetIsPlayerControlled(int iPartyIndex, bool isPlayerControlled);

        #region Game state access
        MobileParty GetMobilePartyByIndex(int iPartyIndex);
        Campaign GetCurrentCampaign();
        #endregion
    }
}
