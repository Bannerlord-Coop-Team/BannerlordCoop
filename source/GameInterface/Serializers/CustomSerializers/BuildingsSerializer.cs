using Common;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;

namespace Coop.Mod.Serializers.Custom
{
    [Serializable]
    public class BuildingsSerializer : CustomSerializerWithGuid
    {
        [NonSerialized]
        Building newBuilding;

        readonly Dictionary<FieldInfo, Guid> references = new Dictionary<FieldInfo, Guid>();

        string typeId;

        public BuildingsSerializer(Building building) : base(building)
        {
            List<FieldInfo> UnmanagedFields = new List<FieldInfo>();

            foreach (FieldInfo fieldInfo in NonSerializableObjects)
            {
                // Get value from fieldInfo
                object value = fieldInfo.GetValue(building);

                // If value is null, no need to serialize
                if (value == null)
                {
                    continue;
                }

                // Assign serializer to nonserializable objects
                switch (fieldInfo.Name)
                {
                    case "BuildingType":
                        typeId = ((BuildingType)value).StringId;
                        break;
                    // References
                    case "<Town>k__BackingField":
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
            BuildingType buildingType = Campaign.Current.ObjectManager.GetObject<BuildingType>(typeId);
            newBuilding = new Building(buildingType, null);
            return base.Deserialize(newBuilding);
        }

        public override void ResolveReferenceGuids()
        {
            if (newBuilding == null)
            {
                throw new NullReferenceException("Deserialize() has not been called before ResolveReferenceGuids().");
            }

            foreach (KeyValuePair<FieldInfo, Guid> entry in references)
            {
                FieldInfo field = entry.Key;
                Guid id = entry.Value;

                field.SetValue(newBuilding, CoopObjectManager.GetObject(id));
            }
        }
    }
}