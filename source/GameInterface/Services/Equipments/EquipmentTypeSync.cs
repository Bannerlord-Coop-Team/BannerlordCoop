using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.Core;

namespace GameInterface.Services.Equipments;

class EquipmentSync : IAutoSync
{
    public EquipmentSync(AutoSyncRegistry autoSyncBuilder)
    {
        autoSyncBuilder.AddField(AccessTools.Field(typeof(Equipment), nameof(Equipment._equipmentType)));
    }
}
