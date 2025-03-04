using GameInterface.AutoSync;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Alleys
{
    public class AlleySync : IAutoSync
    {
        public AlleySync(IAutoSyncBuilder autoSyncBuilder) 
        {
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Alley), nameof(Alley._owner)));
        }
    }
}
