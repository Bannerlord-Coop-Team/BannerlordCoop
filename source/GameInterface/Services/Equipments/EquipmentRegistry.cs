using GameInterface.Services.Registry;
using System;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper;
using TaleWorlds.CampaignSystem.Siege;
using System.Threading;

namespace GameInterface.Services.Equipments;

/// <summary>
/// Registry for <see cref="Equipment"/> objects
/// </summary>
internal class EquipmentRegistry : RegistryBase<Equipment> {

    private const string EquipmentPrefix = $"Coop{nameof(Equipment)}";
    private static int InstanceCounter = 0;

    public EquipmentRegistry(IRegistryCollection collection) : base(collection) { }

    public override void RegisterAll()
    {
        var objectManager = MBObjectManager.Instance;


        if (objectManager == null)
        {
            Logger.Error("Unable to register objects when CampaignObjectManager is null");
            return;
        }

        // Not sure if this can be skipped since due constructor patching all equipment will already be registered.
        foreach (var equipmentRoster in Campaign.Current.AllEquipmentRosters)
        {
            if (equipmentRoster == null) continue;
            foreach (Equipment equipment in equipmentRoster.AllEquipments)
            {
                if (TryGetId(equipment, out _)) {

                    continue;
                }
                RegisterNewObject(equipment, out var _);
            }
        }
    }

    protected override string GetNewId(Equipment equipment)
    {
        return EquipmentPrefix + Interlocked.Increment(ref InstanceCounter);
    }
}