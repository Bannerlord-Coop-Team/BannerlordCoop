using GameInterface.AutoSync;
using GameInterface.DynamicSync;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEngines
{
    internal class SiegeEnginesContainerSync: IDynamicSync
    {
        public SiegeEnginesContainerSync(DynamicSyncRegistry autoSyncBuilder)
        {
            // Fields
            //autoSyncBuilder.AddField(AccessTools.Field(typeof(SiegeEnginesContainer), nameof(SiegeEnginesContainer.SiegePreparations)));
        }
    }
}