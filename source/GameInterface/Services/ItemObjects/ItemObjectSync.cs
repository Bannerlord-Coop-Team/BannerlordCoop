using System;
using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.Core;

namespace GameInterface.Services.ItemObjects
{
    public class ItemObjectSync : IAutoSync
    {
        public ItemObjectSync(IAutoSyncBuilder autoSyncBuilder)
        {
            autoSyncBuilder.AddField(AccessTools.Field(typeof(ItemObject), nameof(ItemObject.Type)));
        }
    }
}
