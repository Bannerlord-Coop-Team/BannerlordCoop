//using System;
//using System.Collections.Generic;
//using System.Reflection;
//using TaleWorlds.CampaignSystem;
//using TaleWorlds.Core;
//using TaleWorlds.ObjectSystem;

//namespace GameInterface.Serializers.CustomSerializers
//{
//    [Serializable]
//    public class CharacterObjectSerializer : CustomSerializerBase
//    {
//        public override Type CustomType => typeof(CharacterObject);

//        [NonSerialized]
//        CharacterObject characterObject;

//        /// <summary>
//        /// Serialized Natively Non Serializable Objects (SNNSO)
//        /// </summary>
//        Dictionary<FieldInfo, ICustomSerializer> SNNSO = new Dictionary<FieldInfo, ICustomSerializer>();

//        Dictionary<FieldInfo, int> references = new Dictionary<FieldInfo, int>();

//        string stringId;

//        public CharacterObjectSerializer(SerializableFactory serializableFactory, ReferenceRepository referenceRepository) : base(serializableFactory, referenceRepository)
//        {
//        }

//        public override byte[] Serialize(object obj)
//        {
//            stringId = characterObject.StringId;

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

//                    case "<BodyPropertyRange>k__BackingField":
//                    case "_culture":
//                    case "_equipmentRoster":
//                    case "_basicName":
//                    case "_heroObject":
//                    case "_characterTraits":
//                    case "CharacterSkills":
//                    case "_persona":
//                        SNNSO.Add(fieldInfo, SerializableFactory.GetSerializer(value));
//                        references.Add(fieldInfo, ReferenceRepo.AddReference(value));
//                        break;
//                    case "_originCharacter":
//                        // Probably not needed, could use string id
//                        break;
//                    case "<UpgradeRequiresItemFromCategory>k__BackingField":
//                        // TODO
//                        break;

//                    default:
//                        UnmanagedFields.Add(fieldInfo.Name);
//                        break;

//                }
//            }

//            // TODO manage collections

//            if (!UnmanagedFields.IsEmpty())
//            {
//                throw new NotImplementedException($"Cannot serialize {UnmanagedFields}");
//            }

//            throw new NotImplementedException();
//        }

//        public override object Deserialize()
//        {
//            characterObject = MBObjectManager.Instance.CreateObject<CharacterObject>(stringId);

//            // Objects requiring a custom serializer
//            foreach (KeyValuePair<FieldInfo, ICustomSerializer> entry in SNNSO)
//            {
//            }

//            throw new NotImplementedException();
//        }

//        public override void ResolveReferences(object obj)
//        {
//            //if (characterObject == null)
//            //{
//            //    throw new NullReferenceException("Deserialize() has not been called before ResolveReferenceGuids().");
//            //}

//            //Hero hero = CoopObjectManager.GetObject<Hero>(this.hero);

//            //typeof(CharacterObject)
//            //    .GetField("_heroObject", BindingFlags.Instance | BindingFlags.NonPublic)
//            //    .SetValue(characterObject, hero);
//        }
//    }
//}
