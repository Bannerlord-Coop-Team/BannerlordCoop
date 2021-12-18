using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Serializers.Custom
{
    [Serializable]
    public class CharacterObjectSerializer : CustomSerializerWithGuid
    {
        [NonSerialized]
        CharacterObject characterObject;

        /// <summary>
        /// Serialized Natively Non Serializable Objects (SNNSO)
        /// </summary>
        Dictionary<FieldInfo, ICustomSerializer> SNNSO = new Dictionary<FieldInfo, ICustomSerializer>();

        Guid hero;

        public CharacterObjectSerializer(CharacterObject characterObject) : base(characterObject)
        {
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
                        hero = CoopObjectManager.GetGuid(value);
                        break;

                    case "_characterTraits":
                        SNNSO.Add(fieldInfo, new CharacterTraitsSerializer((CharacterTraits)value));
                        break;

                    case "_characterFeats":
                        SNNSO.Add(fieldInfo, new CharacterFeatsSerializer((CharacterFeats)value));
                        break;

                    case "CharacterSkills":
                        SNNSO.Add(fieldInfo, new MBCharacterSkillsSerializer((MBCharacterSkills)value));
                        break;

                    case "_persona":
                        SNNSO.Add(fieldInfo, new TraitObjectSerializer((TraitObject)value));
                        break;
                    case "_originCharacter":
                        // TODO
                        break;

                    default:
                        throw new NotImplementedException("Cannot serialize " + fieldInfo.Name);
                    
                }
            }

            // TODO manage collections

            // Remove non serializable objects before serialization
            // They are marked as nonserializable in CustomSerializer but still tries to serialize???
            NonSerializableCollections.Clear();
            NonSerializableObjects.Clear();
        }

        public override object Deserialize()
        {
            characterObject = MBObjectManager.Instance.CreateObject<CharacterObject>();

            // Objects requiring a custom serializer
            foreach (KeyValuePair<FieldInfo, ICustomSerializer> entry in SNNSO)
            {
                entry.Key.SetValue(characterObject, entry.Value.Deserialize());
            }

            return base.Deserialize(characterObject);
        }

        public override void ResolveReferenceGuids()
        {
            if (characterObject == null)
            {
                throw new NullReferenceException("Deserialize() has not been called before ResolveReferenceGuids().");
            }

            Hero hero = CoopObjectManager.GetObject<Hero>(this.hero);

            typeof(CharacterObject)
                .GetField("_heroObject", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(characterObject, hero);
        }
    }
}
