using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using static TaleWorlds.CampaignSystem.Settlements.Workshops.WorkshopType;

namespace Coop.Mod.Serializers.Custom
{
    [Serializable]
    public class ProductionSerializer : CustomSerializer
    {
        Dictionary<ItemCategorySerializer, int> inputs = new Dictionary<ItemCategorySerializer, int>();
        Dictionary<ItemCategorySerializer, int> outputs = new Dictionary<ItemCategorySerializer, int>();

        FieldInfo inputsField;
        FieldInfo outputsField;
        public ProductionSerializer(Production production) : base(production)
        {
            List<FieldInfo> UnmanagedFields = new List<FieldInfo>();

            foreach (FieldInfo fieldInfo in NonSerializableObjects)
            {
                // Get value from fieldInfo
                object value = fieldInfo.GetValue(production);

                // If value is null, no need to serialize
                if (value == null)
                {
                    continue;
                }

                // Assign serializer to nonserializable objects
                switch (fieldInfo.Name)
                {
                    case "_inputs":
                        inputsField = fieldInfo;
                        List<ValueTuple<ItemCategory, int>> _inputs = (List<ValueTuple<ItemCategory, int>>)value;
                        inputs = _inputs.ToDictionary(input => new ItemCategorySerializer(input.Item1), input => input.Item2);
                        break;
                    case "_outputs":
                        outputsField = fieldInfo;
                        List<ValueTuple<ItemCategory, int>> _outputs = (List<ValueTuple<ItemCategory, int>>)value;
                        outputs = _outputs.ToDictionary(input => new ItemCategorySerializer(input.Item1), input => input.Item2);
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
            Production production = new Production();
            if(inputsField != null)
            {
                List<ValueTuple<ItemCategory, int>> _inputs = inputs
                    .Select(input => 
                        new ValueTuple<ItemCategory,int>(
                            (ItemCategory)input.Key.Deserialize(), input.Value)
                        ).ToList();
                inputsField.SetValue(production, _inputs);
            }
            if(outputsField != null)
            {
                List<ValueTuple<ItemCategory, int>> _outputs = outputs
                    .Select(output =>
                        new ValueTuple<ItemCategory, int>(
                            (ItemCategory)output.Key.Deserialize(), output.Value)
                        ).ToList();
                outputsField.SetValue(production, _outputs);
            }

            return base.Deserialize(production);
        }

        public override void ResolveReferenceGuids()
        {
            // No references
        }
    }
}