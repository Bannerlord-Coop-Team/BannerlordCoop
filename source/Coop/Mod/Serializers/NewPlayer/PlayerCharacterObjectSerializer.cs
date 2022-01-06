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
using TaleWorlds.ObjectSystem;
using System.Runtime.Serialization;

namespace Coop.Mod.Serializers
{
    [Serializable]
    public class PlayerCharacterObjectSerializer : CustomSerializer
    {
        /// <summary>
        /// Used for circular reference
        /// </summary>
        [NonSerialized]
        private Hero heroObject;

        /// <summary>
        /// Serialized Natively Non Serializable Objects (SNNSO)
        /// </summary>
        Dictionary<FieldInfo, ICustomSerializer> SNNSO = new Dictionary<FieldInfo, ICustomSerializer>();
        

        public PlayerCharacterObjectSerializer(CharacterObject characterObject) : base(characterObject)
        {
            List<string> UnmanagedFields = new List<string>();

            foreach (FieldInfo fieldInfo in NonSerializableObjects)
            {
                // Get value from fieldInfo
                object value = fieldInfo.GetValue(characterObject);

                // If value is null, no need to serialize
                if (value == null)
                {
                    continue;
                }

                // Assign serializer to nonserializable objects
                switch (fieldInfo.Name)
                {
                    case "_heroObject":
                        // Assigned by SetHeroReference on deserialization
                        break;

                    case "_characterTraits":
                        SNNSO.Add(fieldInfo, new CharacterTraitsSerializer((CharacterTraits)value));
                        break;

                    case "_characterFeats":
                        SNNSO.Add(fieldInfo, new CharacterFeatsSerializer((CharacterFeats)value));
                        break;

                    case "CharacterSkills":
                        SNNSO.Add(fieldInfo, new Custom.MBCharacterSkillsSerializer((MBCharacterSkills)value));
                        break;

                    case "_persona":
                        SNNSO.Add(fieldInfo, new Custom.TraitObjectSerializer((TraitObject)value));
                        break;

                    default:
                        UnmanagedFields.Add(fieldInfo.Name);
                        break;
                        

                }

                if (!UnmanagedFields.IsEmpty())
                {
                    throw new NotImplementedException($"Cannot serialize {UnmanagedFields}");
                }
            }

            // TODO manage collections

            // Remove non serializable objects before serialization
            // They are marked as nonserializable in CustomSerializer but still tries to serialize???
            NonSerializableCollections.Clear();
            NonSerializableObjects.Clear();
        }

        /// <summary>
        /// For assigning PlayerHeroSerializer reference for deserialization
        /// </summary>
        /// <param name="heroObject">PlayerHeroSerializer used by _heroObject</param>
        public void SetHeroReference(Hero heroObject)
        {
            this.heroObject = heroObject;
        }

        public override object Deserialize()
        {
            CharacterObject characterObject = MBObjectManager.Instance.CreateObject<CharacterObject>();
            CoopObjectManager.AddObject(characterObject);

            // Circular referenced object needs assignment before deserialize
            if(heroObject == null)
            {
                throw new SerializationException("Must set hero reference before deserializing. Use SetHeroReference()");
            }

            // Circular referenced objects
            typeof(CharacterObject)
                .GetField("_heroObject", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(characterObject, heroObject);

            // Objects requiring a custom serializer
            foreach (KeyValuePair<FieldInfo, ICustomSerializer> entry in SNNSO)
            {
                entry.Key.SetValue(characterObject, entry.Value.Deserialize());
            }

            return base.Deserialize(characterObject);
        }

        public override void ResolveReferenceGuids()
        {
            throw new NotImplementedException();
        }
    }
}
