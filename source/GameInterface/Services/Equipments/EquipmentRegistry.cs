using GameInterface.Services.Registry;
using System;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper;

namespace GameInterface.Services.Sieges;

/// <summary>
/// Registry for <see cref="Equipment"/> objects
/// </summary>
internal class EquipmentRegistry : RegistryBase<Equipment> { 
    public EquipmentRegistry(IRegistryCollection collection) : base(collection) { }

    public override void RegisterAll()
    {
        var objectManager = MBObjectManager.Instance;

        if (objectManager == null)
        {
            Logger.Error("Unable to register objects when CampaignObjectManager is null");
            return;
        }
        var characters = objectManager.GetObjectTypeList<CharacterObject>();
        foreach (var character in characters)
        {
            foreach (Equipment equipment in character.AllEquipments)
            {
                RegisterNewObject(equipment, out var _);
            }
        }
    }

    protected override string GetNewId(Equipment equipment)
    {
        return Guid.NewGuid().ToString();
    }
}