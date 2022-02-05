using System;
using TaleWorlds.ObjectSystem;
using TaleWorlds.CampaignSystem;
using System.Reflection;
using System.Collections.Generic;
using TaleWorlds.Core;
using System.Linq;
using TaleWorlds.Localization;

namespace Coop.Mod.Serializers.Custom
{
    [Serializable]
    public class VillageTypeSerializer : CustomSerializer
    {
        [NonSerialized]
        VillageType newVillageType;

        /// <summary>
        /// Serialized Natively Non Serializable Objects (SNNSO)
        /// </summary>
        readonly Dictionary<FieldInfo, ICustomSerializer> SNNSO = new Dictionary<FieldInfo, ICustomSerializer>();
        readonly Dictionary<FieldInfo, Guid> references = new Dictionary<FieldInfo, Guid>();

        string stringId;
        FieldInfo production;
        ValueTuple<ItemObjectSerializer, float>[] productionSerialize;

        public VillageTypeSerializer(MBObjectBase villageType) : base(villageType)
        {
            stringId = villageType.StringId;

            List<string> UnmanagedFields = new List<string>();

            foreach (FieldInfo fieldInfo in NonSerializableObjects)
            {
                // Get value from fieldInfo
                object value = fieldInfo.GetValue(villageType);

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
                    case "ShortName":
                        SNNSO.Add(fieldInfo, new TextObjectSerializer((TextObject)value));
                        break;
                    default:
                        UnmanagedFields.Add(fieldInfo.Name);
                        break;
                }
            }

            foreach (FieldInfo fieldInfo in NonSerializableCollections)
            {
                // Get value from fieldInfo
                object value = fieldInfo.GetValue(villageType);

                // If value is null, no need to serialize
                if (value == null)
                {
                    continue;
                }

                // Assign serializer to nonserializable objects
                switch (fieldInfo.Name)
                {
                    case "_productions":
                        production = fieldInfo;
                        ValueTuple<ItemObject, float>[] _production = (ValueTuple<ItemObject, float>[])value;
                        productionSerialize = _production.Select(
                            production => new ValueTuple<ItemObjectSerializer, float>(
                                new ItemObjectSerializer(production.Item1), production.Item2)
                            ).ToArray();
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
        }

        public override object Deserialize()
        {
            newVillageType = new VillageType(stringId);

            if(production != null)
            {
                ValueTuple<ItemObject, float>[] _production = productionSerialize.Select(
                    production => new ValueTuple<ItemObject, float>(
                        (ItemObject)production.Item1.Deserialize(), production.Item2)
                    ).ToArray();

                production.SetValue(newVillageType, _production);
            }

            foreach (KeyValuePair<FieldInfo, ICustomSerializer> entry in SNNSO)
            {
                entry.Key.SetValue(newVillageType, entry.Value.Deserialize());
            }

            return base.Deserialize(newVillageType);
        }

        public override void ResolveReferenceGuids()
        {
            // No references
        }
    }
}
