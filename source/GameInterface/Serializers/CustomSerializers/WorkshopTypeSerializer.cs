using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using static TaleWorlds.CampaignSystem.Settlements.Workshops.WorkshopType;

namespace Coop.Mod.Serializers.Custom
{
    [Serializable]
    public class WorkshopTypeSerializer : CustomSerializer
    {
        [NonSerialized]
        private WorkshopType workshopType;

        /// <summary>
        /// Serialized Natively Non Serializable Objects (SNNSO)
        /// </summary>
        readonly Dictionary<FieldInfo, ICustomSerializer> SNNSO = new Dictionary<FieldInfo, ICustomSerializer>();
        readonly Dictionary<FieldInfo, Guid> references = new Dictionary<FieldInfo, Guid>();

        readonly FieldInfo productionsFieldInfo;
        readonly ProductionSerializer[] productionSerializers = new ProductionSerializer[0];

        public WorkshopTypeSerializer(WorkshopType workshopType) : base(workshopType)
        {
            List<FieldInfo> UnmanagedFields = new List<FieldInfo>();

            foreach (FieldInfo fieldInfo in NonSerializableObjects)
            {
                // Get value from fieldInfo
                object value = fieldInfo.GetValue(workshopType);

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
                    case "<Name>k__BackingField":
                    case "<JobName>k__BackingField":
                    case "<Description>k__BackingField":
                        SNNSO.Add(fieldInfo, new TextObjectSerializer((TextObject)value));
                        break;
                    case "_productions":
                        productionsFieldInfo = fieldInfo;
                        Production[] _productions = (Production[])value;
                        productionSerializers = _productions.Select(production => new ProductionSerializer(production)).ToArray();
                        break;

                    // References
                    case "_owner":
                    case "_settlement":
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
            workshopType = new WorkshopType();
            if(productionsFieldInfo != null)
            {
                Production[] _productions = productionSerializers
                    .Select(production => (Production)production.Deserialize()).ToArray();
                productionsFieldInfo.SetValue(workshopType, _productions);
            }

            return base.Deserialize(workshopType);
        }

        public override void ResolveReferenceGuids()
        {
            if (workshopType == null)
            {
                throw new NullReferenceException("Deserialize() has not been called before ResolveReferenceGuids().");
            }

            foreach (KeyValuePair<FieldInfo, Guid> entry in references)
            {
                FieldInfo field = entry.Key;
                Guid id = entry.Value;

                field.SetValue(workshopType, CoopObjectManager.GetObject(id));
            }
        }
    }
}