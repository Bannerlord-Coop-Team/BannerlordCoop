//using Common;
//using Coop.Mod.Serializers;
//using System;
//using System.Collections.Generic;
//using System.Reflection;
//using System.Runtime.Serialization;
//using TaleWorlds.CampaignSystem;
//using TaleWorlds.CampaignSystem.CharacterDevelopment;
//using TaleWorlds.Core;
//using TaleWorlds.Localization;
//using TaleWorlds.ObjectSystem;

//namespace GameInterface.Serializers.NewPlayer
//{
//    [Serializable]
//    public class PlayerCharacterObjectSerializer : CustomSerializerBase
//    {
//        /// <summary>
//        /// Used for circular reference
//        /// </summary>
//        [NonSerialized]
//        private Hero heroObject;

//        /// <summary>
//        /// Serialized Natively Non Serializable Objects (SNNSO)
//        /// </summary>
//        Dictionary<FieldInfo, ICustomSerializer> SNNSO = new Dictionary<FieldInfo, ICustomSerializer>();


//        public PlayerCharacterObjectSerializer(CharacterObject characterObject)
//        {
//            CollectObjects(characterObject);

//            List<string> UnmanagedFields = new List<string>();

//            foreach (FieldInfo fieldInfo in NonSerializableObjects)
//            {
//                // Get value from fieldInfo
//                object value = fieldInfo.GetValue(characterObject);

//                // If value is null, no need to serialize
//                if (value == null)
//                {
//                    continue;
//                }

//                // Assign serializer to nonserializable objects
//                switch (fieldInfo.Name)
//                {
//                    case "<Id>k__BackingField":
//                        // Ignore current MB id
//                        break;

//                    case "_heroObject":
//                        // Assigned by SetHeroReference on deserialization
//                        break;

//                    case "<BodyPropertyRange>k__BackingField":
//                        // Cached object
//                        break;

//                    case "_culture":
//                        // TODO create serializer
//                        break;

//                    case "_equipmentRoster":
//                        // TODO create serializer
//                        break;


//                    case "_basicName":
//                        SNNSO.Add(fieldInfo, new TextObjectSerializer((TextObject)value));
//                        break;

//                    case "_characterTraits":
//                        SNNSO.Add(fieldInfo, new CharacterTraitsSerializer((CharacterTraits)value));
//                        break;

//                    case "CharacterSkills":
//                        SNNSO.Add(fieldInfo, new Custom.MBCharacterSkillsSerializer((MBCharacterSkills)value));
//                        break;

//                    case "_persona":
//                        SNNSO.Add(fieldInfo, new Custom.TraitObjectSerializer((TraitObject)value));
//                        break;

//                    default:
//                        UnmanagedFields.Add(fieldInfo.Name);
//                        break;


//                }
//            }

//            if (!UnmanagedFields.IsEmpty())
//            {
//                throw new NotImplementedException($"Cannot serialize {UnmanagedFields}");
//            }

//            // TODO manage collections

//            // Remove non serializable objects before serialization
//            // They are marked as nonserializable in CustomSerializer but still tries to serialize???
//            NonSerializableCollections.Clear();
//            NonSerializableObjects.Clear();
//        }

//        /// <summary>
//        /// For assigning PlayerHeroSerializer reference for deserialization
//        /// </summary>
//        /// <param name="heroObject">PlayerHeroSerializer used by _heroObject</param>
//        public void SetHeroReference(Hero heroObject)
//        {
//            this.heroObject = heroObject;
//        }

//        public override object Deserialize()
//        {
//            CharacterObject characterObject = MBObjectManager.Instance.CreateObject<CharacterObject>();
//            CoopObjectManager.AddObject(characterObject);

//            // Circular referenced object needs assignment before deserialize
//            if (heroObject == null)
//            {
//                throw new SerializationException("Must set hero reference before deserializing. Use SetHeroReference()");
//            }

//            // Circular referenced objects
//            typeof(CharacterObject)
//                .GetField("_heroObject", BindingFlags.Instance | BindingFlags.NonPublic)
//                .SetValue(characterObject, heroObject);

//            // Objects requiring a custom serializer
//            foreach (KeyValuePair<FieldInfo, ICustomSerializer> entry in SNNSO)
//            {
//                entry.Key.SetValue(characterObject, entry.Value.Deserialize());
//            }

//            return base.Deserialize(characterObject);
//        }

//        public override void ResolveReferences()
//        {
//            throw new NotImplementedException();
//        }
//    }
//}
