using Common;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using static TaleWorlds.CampaignSystem.Settlements.Town;

namespace Coop.Mod.Serializers.Custom
{
    [Serializable]
    public class SellLogSerializer : CustomSerializer
    {
        [NonSerialized]
        SellLog sellLog;

        readonly Dictionary<FieldInfo, Guid> references = new Dictionary<FieldInfo, Guid>();
        public SellLogSerializer(SellLog sellLog) 
        {
            List<FieldInfo> UnmanagedFields = new List<FieldInfo>();

            foreach (FieldInfo fieldInfo in NonSerializableObjects)
            {
                // Get value from fieldInfo
                object value = fieldInfo.GetValue(sellLog);

                // If value is null, no need to serialize
                if (value == null)
                {
                    continue;
                }

                // Assign serializer to nonserializable objects
                switch (fieldInfo.Name)
                {
                    case "Category":
                        references.Add(fieldInfo, CoopObjectManager.GetGuid(value));
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
            sellLog = new SellLog();
            return base.Deserialize(sellLog);
        }

        public override void ResolveReferenceGuids()
        {
            foreach (KeyValuePair<FieldInfo, Guid> entry in references)
            {
                FieldInfo field = entry.Key;
                Guid id = entry.Value;

                field.SetValue(sellLog, CoopObjectManager.GetObject(id));
            }
        }
    }
}