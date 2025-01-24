using System;
using System.Collections.Generic;
using System.Text;
using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents
{
    internal class MapEventSync : IAutoSync
    {
        public MapEventSync(IAutoSyncBuilder autoSyncBuilder)
        {
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEvent), nameof(MapEvent.AttackersRanAway)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEvent), nameof(MapEvent.BattleObserver)));
        }
    }
}
