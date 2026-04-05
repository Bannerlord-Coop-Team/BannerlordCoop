using System;
using GameInterface.AutoSync;
using GameInterface.DynamicSync;
using HarmonyLib;
using TaleWorlds.Core;

namespace GameInterface.Services.ItemObjects
{
    public class ItemObjectSync : IDynamicSync
    {
        public ItemObjectSync(DynamicSyncRegistry autoSyncBuilder)
        {
            autoSyncBuilder.AddField(AccessTools.Field(typeof(ItemObject), nameof(ItemObject.Type)));
        }
    }
}
