using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace E2E.Tests.Util.ObjectBuilders;
internal class CharacterObjectBuilder : IObjectBuilder
{
    public object Build()
    {
        var characterObject = new CharacterObject();

        characterObject._basicName = new TextObject("Test Character");
        characterObject.DefaultCharacterSkills = new MBCharacterSkills();

        //List<Equipment> list = characterObject.AllEquipments.Where((Equipment t) => !t.IsEmpty() && !t.IsCivilian).ToList();
        //List<Equipment> list2 = characterObject.AllEquipments.Where((Equipment t) => !t.IsEmpty() && t.IsCivilian).ToList();

        var battleEquipment = new Equipment(Equipment.EquipmentType.Battle);
        var civilianEquipment = new Equipment(Equipment.EquipmentType.Civilian);

        // Weapon slots stay empty: these placeholder items have no WeaponComponent, and building
        // an agent origin (e.g. SimpleAgentOrigin) reads the first battle equipment's weapon slots
        // and dereferences PrimaryWeapon, which is null on a bare item. Armor and mount slots
        // (indices 5-11) are still populated.
        for (int i = (int)EquipmentIndex.NumAllWeaponSlots; i < (int)EquipmentIndex.NumEquipmentSetSlots; i++)
        {
            ItemObject battleItem = new ItemObject($"BattleItem_{i}");
            //battleItem.Effectiveness = i;
            battleEquipment[i] = new EquipmentElement(battleItem);

            ItemObject civItem = new ItemObject($"CivItem_{i}");
            //battleItem.Difficulty = i;
            civilianEquipment[i] = new EquipmentElement(civItem);
        }

        var equipment = new MBList<Equipment>()
        {
            battleEquipment,
            civilianEquipment
        };


        MBEquipmentRoster equipmentRoster = new MBEquipmentRoster();

        equipmentRoster._equipments = equipment;

        characterObject._equipmentRoster = equipmentRoster;
        characterObject.BodyPropertyRange = new MBBodyProperty();

        characterObject.Culture = (CultureObject)(new CultureBuilder().Build());

        characterObject.DefaultCharacterSkills = new MBCharacterSkills();

        return characterObject;
    }
}
