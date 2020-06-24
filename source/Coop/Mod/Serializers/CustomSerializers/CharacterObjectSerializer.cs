using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using System.Reflection;
using System.Xml;
using TaleWorlds.Core;
using System.IO;
using System.Collections;

namespace Coop.Mod.Serializers
{
    [Serializable]
    class CharacterObjectSerializer : CustomSerializer
    {
        //XmlDocument document;
        HeroSerializer heroSerializer;

        /// <summary>
        /// Serialized Natively Non Serializable Objects (SNNSO)
        /// </summary>
        Dictionary<FieldInfo, ICustomSerializer> SNNSO = new Dictionary<FieldInfo, ICustomSerializer>();

        public CharacterObjectSerializer() { }

        public CharacterObjectSerializer(CharacterObject characterObject, HeroSerializer hero) : base(characterObject)
        {
            foreach (FieldInfo fieldInfo in NonSerializableObjects)
            {
                switch (fieldInfo.Name)
                {
                    case "_heroObject":
                        heroSerializer = hero;
                        break;

                    case "_characterTraits":
                        SNNSO.Add(fieldInfo, new CharacterTraitsSerializer((CharacterTraits)fieldInfo.GetValue(characterObject)));
                        break;

                    case "_characterFeats":
                        SNNSO.Add(fieldInfo, new CharacterFeatsSerializer((CharacterFeats)fieldInfo.GetValue(characterObject)));
                        break;

                    case "_characterSkills":
                        CharacterSkills characterSkills = (CharacterSkills)fieldInfo.GetValue(characterObject);
                        SNNSO.Add(fieldInfo, new CharacterSkillsSerializer(characterSkills));
                        break;

                    default:
                        throw new NotImplementedException("Cannot serialize " + fieldInfo.Name);
                    
                }
            }
        }

        public override object Deserialize()
        {
            CharacterObject characterObject = new CharacterObject();

            // Cross referenced objects
            typeof(CharacterObject)
                .GetField("_heroObject", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(characterObject, heroSerializer.hero);

            // Objects natively serializable
            foreach (FieldInfo fieldInfo in SerializableObjects.Keys)
            {
                fieldInfo.SetValue(characterObject, SerializableObjects[fieldInfo]);
            }

            // Objects requiring a custom serializer
            foreach (FieldInfo fieldInfo in SNNSO.Keys)
            {
                fieldInfo.SetValue(characterObject, SNNSO[fieldInfo].Deserialize());
            }

            foreach (ICollection iterable in Collections)
            {
                int count = iterable.Count;
            }

            return characterObject;
        }
    }
}
