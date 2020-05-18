using System;
using System.IO;
using System.Xml;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace MBMultiplayerCampaign.Serializers
{
    [Serializable]
    public class CharacterTraitsSerializer : CustomSerializer
    {
        string data;
        public CharacterTraitsSerializer()
        {
        }

        public CharacterTraitsSerializer(CharacterTraits characterTraits) : base(characterTraits)
        {
            StringWriter stringWriter = new StringWriter();
            XmlTextWriter xmlWriter = new XmlTextWriter(stringWriter);
            characterTraits.Serialize(xmlWriter);
            data = stringWriter.ToString();
        }

        public override object Deserialize()
        {
            StringReader stringReader = new StringReader(data);
            XmlTextReader xmlReader = new XmlTextReader(stringReader);
            XmlDocument doc = new XmlDocument();
            XmlNode node = doc.ReadNode(xmlReader);
            CharacterTraits characterTraits = new CharacterTraits();
            characterTraits.Deserialize(Game.Current.ObjectManager, node);
            return characterTraits;
        }

        public override ICustomSerializer Serialize(object obj)
        {
            
            throw new System.NotImplementedException();
        }
    }
}