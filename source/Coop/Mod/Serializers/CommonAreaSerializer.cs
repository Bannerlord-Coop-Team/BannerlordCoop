using Common;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Coop.Mod.Serializers.Custom
{
    [Serializable]
    public class CommonAreaSerializer : CustomSerializer
    {
        [NonSerialized]
        private CommonArea commonArea;

        /// <summary>
        /// Serialized Natively Non Serializable Objects (SNNSO)
        /// </summary>
        Dictionary<FieldInfo, ICustomSerializer> SNNSO = new Dictionary<FieldInfo, ICustomSerializer>();
        readonly Dictionary<FieldInfo, Guid> references = new Dictionary<FieldInfo, Guid>();

        public CommonAreaSerializer(CommonArea commonArea) : base(commonArea)
        {
            List<FieldInfo> UnmanagedFields = new List<FieldInfo>();

            foreach (FieldInfo fieldInfo in NonSerializableObjects)
            {
                // Get value from fieldInfo
                object value = fieldInfo.GetValue(commonArea);

                // If value is null, no need to serialize
                if (value == null)
                {
                    continue;
                }

                // Assign serializer to nonserializable objects
                switch (fieldInfo.Name)
                {
                    case "_name":
                        SNNSO.Add(fieldInfo, new TextObjectSerializer((TextObject)value));
                        break;
                    case "_settlement":
                    case "<CommonAreaPartyComponent>k__BackingField":
                        references.Add(fieldInfo, CoopObjectManager.GetGuid(value));
                        break;
                    default:
                        UnmanagedFields.Add(fieldInfo);
                        break;
                }

                if (!UnmanagedFields.IsEmpty())
                {
                    throw new NotImplementedException($"Cannot serialize {UnmanagedFields}");
                }
            }
        }

        public override object Deserialize()
        {
            commonArea = new CommonArea(null, "", CommonArea.CommonAreaType.Backstreet, new TextObject(""));
            return base.Deserialize(commonArea);
        }

        public override void ResolveReferenceGuids()
        {
            // TODO
        }
    }
}