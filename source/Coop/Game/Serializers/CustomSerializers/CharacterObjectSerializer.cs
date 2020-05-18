//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using TaleWorlds.CampaignSystem;
//using System.Reflection;
//using System.Xml;
//using TaleWorlds.Core;
//using System.IO;

//namespace MBMultiplayerCampaign.Serializers
//{
//    [Serializable]
//    class CharacterObjectSerializer : ICustomSerializer
//    {
//        XmlDocument document;
//        HeroSerializer heroObject;
//        SerializableObject characterObject;

//        public CharacterObjectSerializer() { }

//        public CharacterObjectSerializer(CharacterObject characterObject) 
//        {
//            StringWriter stringWriter = new StringWriter();
//            XmlTextWriter textWriter = new XmlTextWriter(stringWriter);
//            this.characterObject = MBSerializer.Serialize(characterObject);
//            heroObject = new HeroSerializer(characterObject.HeroObject);
//        }

//        public ICustomSerializer Serialize(object obj)
//        {
//            return new CharacterObjectSerializer((CharacterObject)obj);
//        }

//        object ICustomSerializer.Deserialize()
//        {


//            CharacterObject character = MBSerializer.DeserializeObject(characterObject) as CharacterObject;
//            character.Deserialize(Game.Current.ObjectManager, document.ChildNodes[0]);
//            return character;
//        }
//    }
//}
