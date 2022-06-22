using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Serializers
{
    [Serializable]
    internal class PlayerHeroDeveloperSerializer : CustomSerializer
    {
        /// <summary>
        /// Used for circular reference
        /// </summary>
        [NonSerialized]
        public Hero hero;


        /// <summary>
        /// Serialized Natively Non Serializable Objects (SNNSO)
        /// </summary>
        Dictionary<FieldInfo, ICustomSerializer> SNNSO = new Dictionary<FieldInfo, ICustomSerializer>();

        public PlayerHeroDeveloperSerializer(HeroDeveloper heroDeveloper) : base(heroDeveloper) 
        {
            List<string> UnmanagedFields = new List<string>();

            foreach (FieldInfo fieldInfo in NonSerializableObjects)
            {
                // Get value from fieldInfo
                object value = fieldInfo.GetValue(heroDeveloper);

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
                    case "_newFocuses":
                        SNNSO.Add(fieldInfo, new CharacterSkillsSerializer((CharacterSkills)value));
                        break;
                    case "<Hero>k__BackingField":
                        break;
                    default:
                        UnmanagedFields.Add(fieldInfo.Name);
                        break;
                }
            }

            if (!UnmanagedFields.IsEmpty())
            {
                throw new NotImplementedException($"Cannot serialize {UnmanagedFields}");
            }

            // TODO manage collections

            // Remove non serializable objects before serialization
            // They are marked as nonserializable in CustomSerializer but still tries to serialize???
            NonSerializableCollections.Clear();
            NonSerializableObjects.Clear();
        }

        public void SetHeroReference(Hero hero)
        {
            this.hero = hero;
        }

        public override object Deserialize()
        {
            // Circular referenced object needs assignment before deserialize
            if (hero == null)
            {
                throw new SerializationException("Must set hero reference before deserializing. Use SetHeroReference()");
            }

            ConstructorInfo ctor = typeof(HeroDeveloper).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(Hero) }, null);
            HeroDeveloper newHeroDeveloper = (HeroDeveloper)ctor.Invoke(new object[] { hero });

            foreach (KeyValuePair<FieldInfo, ICustomSerializer> entry in SNNSO)
            {
                entry.Key.SetValue(newHeroDeveloper, entry.Value.Deserialize());
            }

            return newHeroDeveloper;
        }

        public override void ResolveReferenceGuids()
        {
            // Do nothing
        }
    }
}