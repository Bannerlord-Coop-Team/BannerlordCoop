using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using TaleWorlds.Localization;
using Common;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

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

        string stringId;

        Guid hero;

        public CharacterObjectSerializer(CharacterObject characterObject) : base(characterObject)
        {
            stringId = characterObject.StringId;

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
                    case "<Id>k__BackingField":
                        // Ignore current MB id
                        break;

                    case "<BodyPropertyRange>k__BackingField":
                        //SNNSO.Add(fieldInfo, new MBBodyPropertySerializer((MBBodyProperty)value));
                        break;

                    case "_culture":
                        // TODO create serializer
                        break;

                    case "_equipmentRoster":
                        // TODO create serializer
                        break;

                    case "_basicName":
                        SNNSO.Add(fieldInfo, new TextObjectSerializer((TextObject)value));
                        break;

                    case "_heroObject":
                        hero = CoopObjectManager.GetGuid(value);
                        break;

                    case "_characterTraits":
                        SNNSO.Add(fieldInfo, new CharacterTraitsSerializer((CharacterTraits)value));
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
                    case "<UpgradeRequiresItemFromCategory>k__BackingField":
                        // TODO
                        break;

                    default:
                        UnmanagedFields.Add(fieldInfo.Name);
                        break;

                }
            }

            // TODO manage collections

            if (!UnmanagedFields.IsEmpty())
            {
                throw new NotImplementedException($"Cannot serialize {UnmanagedFields}");
            }
        }

        public override object Deserialize()
        {
            characterObject = MBObjectManager.Instance.CreateObject<CharacterObject>(stringId);

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
