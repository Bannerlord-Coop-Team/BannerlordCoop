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

        var battleEquipment = new Equipment(false);
        var civilianEquipment = new Equipment(true);

        for (int i = 0; i < 12; i++)
        {
            ItemObject battleItem = new ItemObject();
            //battleItem.Effectiveness = i;
            battleEquipment[i] = new EquipmentElement(battleItem);

            ItemObject civItem = new ItemObject();
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
