using GameInterface.AutoSync;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.BesiegerCamps
{
    class BesiegerCampSync : IAutoSync
    {
        public BesiegerCampSync(IAutoSyncBuilder autoSyncBuilder)
        {
            autoSyncBuilder.AddField(AccessTools.Field(typeof(BesiegerCamp), nameof(BesiegerCamp._leaderParty)));
        }
    }
}
