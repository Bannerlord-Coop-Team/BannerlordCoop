using GameInterface.AutoSync;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Towns;
internal class TownSync : IAutoSync
{
    public TownSync(IAutoSyncBuilder autoSyncBuilder)
    {
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Town), nameof(Town.Governor)));
    }
}
