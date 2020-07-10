using System;
using System.Xml;
using TaleWorlds.Core;

namespace Coop.Mod.Serializers
{
    [Serializable]
    public class CharacterSkillsSerializer : PropertyOwnerSerializer<SkillObject>
    {
        public CharacterSkillsSerializer(CharacterSkills characterSkills) : base(characterSkills) { }

        public override object Deserialize()
        {
            XmlNode node = (XmlNode)base.Deserialize();
            CharacterSkills characterSkills = new CharacterSkills();
            characterSkills.Deserialize(Game.Current.ObjectManager, node);
            return characterSkills;
        }
    }
}