using Common;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;

namespace Coop.Mod.Serializers.Custom
{
    [Serializable]
    public class HeroDeveloperSerializer : CustomSerializer
    {
        [NonSerialized]
        HeroDeveloper heroDeveloper;

        /// <summary>
        /// Serialized Natively Non Serializable Objects (SNNSO)
        /// </summary>
        readonly Dictionary<FieldInfo, ICustomSerializer> SNNSO = new Dictionary<FieldInfo, ICustomSerializer>();
        readonly Dictionary<FieldInfo, Guid> References = new Dictionary<FieldInfo, Guid>();
        public HeroDeveloperSerializer(HeroDeveloper developer) : base(developer)
        {
            List<FieldInfo> UnmanagedFields = new List<FieldInfo>();

            foreach (FieldInfo fieldInfo in NonSerializableObjects)
            {
                // Get value from fieldInfo
                object value = fieldInfo.GetValue(developer);

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
                        References.Add(fieldInfo, CoopObjectManager.GetGuid(value));
                        break;
                    default:
                        UnmanagedFields.Add(fieldInfo);
                        break;

                }
            }

            if (!UnmanagedFields.IsEmpty())
            {
                throw new NotImplementedException($"Cannot serialize {UnmanagedFields}");
            }

        }

        public override object Deserialize()
        {
            ConstructorInfo ctor = typeof(HeroDeveloper)
                .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(Hero) }, null);

            heroDeveloper = (HeroDeveloper)ctor.Invoke(new[] { (Hero)null });

            foreach (KeyValuePair<FieldInfo, ICustomSerializer> entry in SNNSO)
            {
                entry.Key.SetValue(heroDeveloper, entry.Value.Deserialize());
            }

            base.Deserialize(heroDeveloper);

            return heroDeveloper;
        }

        public override void ResolveReferenceGuids()
        {
            if (heroDeveloper == null)
            {
                throw new NullReferenceException("Deserialize() has not been called before ResolveReferenceGuids().");
            }

            foreach (KeyValuePair<FieldInfo, Guid> entry in References)
            {
                FieldInfo field = entry.Key;
                Guid id = entry.Value;

                field.SetValue(heroDeveloper, CoopObjectManager.GetObject(id));
            }
        }
    }
}