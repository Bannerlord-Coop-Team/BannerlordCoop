using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Serializers.Custom
{
    // TODO find a way to not use stringId
    [Serializable]
    public class ItemCategorySerializer : ICustomSerializer
    {
        //[NonSerialized]
        //ItemCategory itemCategory;

        //readonly Dictionary<FieldInfo, Guid> references = new Dictionary<FieldInfo, Guid>();
        string stringId;
        public ItemCategorySerializer(ItemCategory itemCategory)
        {
            stringId = itemCategory.StringId;
            //List<FieldInfo> UnmanagedFields = new List<FieldInfo>();

            //foreach (FieldInfo fieldInfo in NonSerializableObjects)
            //{
            //    // Get value from fieldInfo
            //    object value = fieldInfo.GetValue(itemCategory);

            //    // If value is null, no need to serialize
            //    if (value == null)
            //    {
            //        continue;
            //    }

            //    // Assign serializer to nonserializable objects
            //    switch (fieldInfo.Name)
            //    {
            //        case "CanSubstitute":
            //            references.Add(fieldInfo, CoopObjectManager.GetGuid(value));
            //            break;
            //        default:
            //            UnmanagedFields.Add(fieldInfo);
            //            break;
            //    }
            //}

            //if (!UnmanagedFields.IsEmpty())
            //{
            //    throw new NotImplementedException($"Cannot serialize {UnmanagedFields}");
            //}
        }

        public object Deserialize()
        {
            return MBObjectManager.Instance.GetObject<ItemCategory>(stringId);

            //itemCategory = new ItemCategory();
            //return base.Deserialize(itemCategory);
        }

        public void ResolveReferenceGuids()
        {
            //if (itemCategory == null)
            //{
            //    throw new NullReferenceException("Deserialize() has not been called before ResolveReferenceGuids().");
            //}

            //foreach (KeyValuePair<FieldInfo, Guid> entry in references)
            //{
            //    FieldInfo field = entry.Key;
            //    Guid id = entry.Value;

            //    field.SetValue(itemCategory, CoopObjectManager.GetObject(id));
            //}
        }

        public byte[] Serialize()
        {
            throw new NotImplementedException();
        }
    }
}