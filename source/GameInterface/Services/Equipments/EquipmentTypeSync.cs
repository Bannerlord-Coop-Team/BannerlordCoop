using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Equipments;

class EquipmentSync : IAutoSync
{
    public EquipmentSync(IAutoSyncBuilder autoSyncBuilder)
    {
        autoSyncBuilder.AddField(AccessTools.Field(typeof(Equipment), nameof(Equipment._equipmentType)));

    }
}
