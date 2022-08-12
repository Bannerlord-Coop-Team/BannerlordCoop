using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Coop.Mod.Serializers.Custom
{
    [Serializable]
    public class HideoutSerializer : CustomSerializer
    {
        /// <summary>
        /// Serialized Natively Non Serializable Objects (SNNSO)
        /// </summary>
        readonly Dictionary<FieldInfo, ICustomSerializer> SNNSO = new Dictionary<FieldInfo, ICustomSerializer>();

        public HideoutSerializer(Hideout hideout)
        {
            List<FieldInfo> UnmanagedFields = new List<FieldInfo>();

            foreach (FieldInfo fieldInfo in NonSerializableObjects)
            {
                // Get value from fieldInfo
                object value = fieldInfo.GetValue(hideout);

                // If value is null, no need to serialize
                if (value == null)
                {
                    continue;
                }

                // Assign serializer to nonserializable objects
                switch (fieldInfo.Name)
                {
                    case "_nextPossibleAttackTime":
                        SNNSO.Add(fieldInfo, new CampaignTimeSerializer((CampaignTime)value));
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
            Hideout newHideout = new Hideout();

            foreach (KeyValuePair<FieldInfo, ICustomSerializer> entry in SNNSO)
            {
                entry.Key.SetValue(newHideout, entry.Value.Deserialize());
            }

            base.Deserialize(newHideout);

            return newHideout;
        }

        public override void ResolveReferenceGuids()
        {
            // No references
        }
    }
}