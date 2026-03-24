using GameInterface.AutoSync;
using GameInterface.DynamicSync;
using HarmonyLib;
using TaleWorlds.Core;

namespace GameInterface.Services.Equipments;

class EquipmentSync : IDynamicSync
{
    public EquipmentSync(DynamicSyncRegistry autoSyncBuilder)
    {
        autoSyncBuilder.AddField(AccessTools.Field(typeof(Equipment), nameof(Equipment._equipmentType)));

    }
}
