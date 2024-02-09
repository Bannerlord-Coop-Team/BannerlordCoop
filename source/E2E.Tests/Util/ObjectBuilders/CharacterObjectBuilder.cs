using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        characterObject.Culture = new CultureObject();
        characterObject.StringId = "";

        AccessTools.Field(typeof(CharacterObject), "_basicName").SetValue(characterObject, new TextObject("Test Character"));
        AccessTools.Field(typeof(CharacterObject), "DefaultCharacterSkills").SetValue(characterObject, new MBCharacterSkills());

        //List<Equipment> list = characterObject.AllEquipments.Where((Equipment t) => !t.IsEmpty() && !t.IsCivilian).ToList();
        //List<Equipment> list2 = characterObject.AllEquipments.Where((Equipment t) => !t.IsEmpty() && t.IsCivilian).ToList();

        var battleEquipment = new Equipment(false);
        var civilianEquipment = new Equipment(true);

        for (int i = 0; i < 12; i++)
        {
            battleEquipment[i] = new EquipmentElement(new ItemObject());
            civilianEquipment[i] = new EquipmentElement(new ItemObject());
        }

        var equipment = new MBList<Equipment>()
        {
            battleEquipment,
            civilianEquipment
        };


        MBEquipmentRoster equipmentRoster = new MBEquipmentRoster();

        AccessTools.Field(typeof(MBEquipmentRoster), "_equipments").SetValue(equipmentRoster, equipment);


        AccessTools.Field(typeof(BasicCharacterObject), "_equipmentRoster").SetValue(characterObject, equipmentRoster);
        AccessTools.Property(typeof(BasicCharacterObject), "BodyPropertyRange").SetValue(characterObject, new MBBodyProperty());

        characterObject.Culture = (CultureObject)(new CultureBuilder().Build());

        return characterObject;
    }
}
