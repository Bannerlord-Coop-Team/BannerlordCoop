using GameInterface.DynamicSync;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEventComponents;

internal class MapEventComponentSync : IDynamicSync
{
    public MapEventComponentSync(DynamicSyncRegistry autoSyncBuilder)
    {
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEventComponent), nameof(MapEventComponent.MapEvent)));

        autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEventComponent), nameof(MapEventComponent._isFinished)));
    }
}
