using GameInterface.AutoSync;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Villages;
internal class VillageAutoSync : IAutoSync
{
    public VillageAutoSync(IAutoSyncBuilder<Village> villageSync)
    {
        villageSync.SyncCreation();
    }
}
