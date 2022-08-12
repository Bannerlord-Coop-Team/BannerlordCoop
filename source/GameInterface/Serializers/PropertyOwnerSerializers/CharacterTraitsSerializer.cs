using System;
using System.IO;
using System.Xml;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;

namespace Coop.Mod.Serializers
{
    [Serializable]
    public class CharacterTraitsSerializer : PropertyOwnerSerializer<TraitObject>
    {

        public CharacterTraitsSerializer(CharacterTraits characterTraits) : base(characterTraits) { }

        public override object Deserialize()
        {
            XmlNode node = (XmlNode)base.Deserialize();
            CharacterTraits characterTraits = new CharacterTraits();
            characterTraits.Deserialize(Game.Current.ObjectManager, node);
            return characterTraits;
        }
    }
}