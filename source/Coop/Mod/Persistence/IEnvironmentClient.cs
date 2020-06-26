using Sync;
using System;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Persistence
{
    public interface IEnvironmentClient
    {
        FieldAccessGroup<MobileParty, MovementData> TargetPosition { get; }
        FieldAccess<Campaign, CampaignTimeControlMode> TimeControlMode { get; }
        FieldAccess<Campaign, bool> TimeControlModeLock { get; }

        #region Game state access
        MobileParty GetMobilePartyByIndex(int iPartyIndex);
        Campaign GetCurrentCampaign();
        #endregion
    }
}
