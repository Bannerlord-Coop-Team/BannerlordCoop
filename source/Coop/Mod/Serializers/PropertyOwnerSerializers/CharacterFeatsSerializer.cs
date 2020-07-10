using System;
using System.Xml;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace Coop.Mod.Serializers
{
    [Serializable]
    public class CharacterFeatsSerializer : PropertyOwnerSerializer<FeatObject>
    {
        public CharacterFeatsSerializer(CharacterFeats characterFeats) : base(characterFeats) { }

        public override object Deserialize()
        {
            XmlNode node = (XmlNode)base.Deserialize();
            CharacterFeats characterFeats = new CharacterFeats();
            characterFeats.Deserialize(Game.Current.ObjectManager, node);
            return characterFeats;
        }
    }
}