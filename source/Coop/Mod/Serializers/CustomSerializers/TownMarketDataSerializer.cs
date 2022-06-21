using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Coop.Mod.Serializers.Custom
{
    [Serializable]
    public class TownMarketDataSerializer : CustomSerializer
    {
        [NonSerialized]
        private TownMarketData townMarketData;

        readonly Dictionary<FieldInfo, Guid> references = new Dictionary<FieldInfo, Guid>();

        FieldInfo itemDictField;
        readonly Dictionary<ItemCategorySerializer, ItemDataSerializer> itemDict = new Dictionary<ItemCategorySerializer, ItemDataSerializer>();

        public TownMarketDataSerializer(TownMarketData townMarketData) : base(townMarketData)
        {
            List<FieldInfo> UnmanagedFields = new List<FieldInfo>();

            foreach (FieldInfo fieldInfo in NonSerializableObjects)
            {
                // Get value from fieldInfo
                object value = fieldInfo.GetValue(townMarketData);

                // If value is null, no need to serialize
                if (value == null)
                {
                    continue;
                }

                // Assign serializer to nonserializable objects
                switch (fieldInfo.Name)
                {
                    case "_itemDict":
                        itemDictField = fieldInfo;
                        Dictionary<ItemCategory, ItemData> _itemDict = (Dictionary<ItemCategory, ItemData>)value;
                        itemDict = _itemDict.ToDictionary(
                            item => new ItemCategorySerializer(item.Key), 
                            item => new ItemDataSerializer(item.Value));
                        break;
                    case "_town":
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
            townMarketData = new TownMarketData(null);
            if (itemDictField != null)
            {
                Dictionary<ItemCategory, ItemData> _itemDict = itemDict
                    .ToDictionary(
                        item => (ItemCategory)item.Key.Deserialize(), 
                        item => (ItemData)item.Value.Deserialize());
                itemDictField.SetValue(townMarketData, _itemDict);
            }

            return base.Deserialize(townMarketData);
        }

        public override void ResolveReferenceGuids()
        {
            if (townMarketData == null)
            {
                throw new NullReferenceException("Deserialize() has not been called before ResolveReferenceGuids().");
            }

            foreach (KeyValuePair<FieldInfo, Guid> entry in references)
            {
                FieldInfo field = entry.Key;
                Guid id = entry.Value;

                field.SetValue(townMarketData, CoopObjectManager.GetObject(id));
            }
        }
    }
}