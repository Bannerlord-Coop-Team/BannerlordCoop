using GameInterface.AutoSync;
using GameInterface.AutoSync;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEngines
{
    internal class SiegeEnginesContainerSync: IAutoSync
    {
        public SiegeEnginesContainerSync(AutoSyncRegistry autoSyncBuilder)
        {
            // Fields
            //autoSyncBuilder.AddField(AccessTools.Field(typeof(SiegeEnginesContainer), nameof(SiegeEnginesContainer.SiegePreparations)));
        }
    }
}