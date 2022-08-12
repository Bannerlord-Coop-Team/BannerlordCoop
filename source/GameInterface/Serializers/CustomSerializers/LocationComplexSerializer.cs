using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;

namespace Coop.Mod.Serializers.Custom
{
    [Serializable]
    public class LocationComplexSerializer : CustomSerializerWithGuid
    {
        [NonSerialized]
        LocationComplex newLocationComplex;

        FieldInfo locationsFieldInfo;
        Dictionary<string, LocationSerializer> locations = new Dictionary<string, LocationSerializer>();
        public LocationComplexSerializer(LocationComplex locationComplex) : base(locationComplex)
        {
            List<FieldInfo> UnmanagedFields = new List<FieldInfo>();

            foreach (FieldInfo fieldInfo in NonSerializableCollections)
            {
                // Get value from fieldInfo
                object value = fieldInfo.GetValue(locationComplex);

                // If value is null, no need to serialize
                if (value == null)
                {
                    continue;
                }

                // Assign serializer to nonserializable objects
                switch (fieldInfo.Name)
                {
                    case "_locations":
                        locationsFieldInfo = fieldInfo;
                        Dictionary<string, Location> _locations = (Dictionary<string, Location>)value;
                        locations = _locations.ToDictionary(
                            location => location.Key, 
                            location => new LocationSerializer(location.Value));
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
            newLocationComplex = new LocationComplex();

            if (locationsFieldInfo != null)
            {
                Dictionary<string, Location> _locations = locations.ToDictionary(
                    loc => loc.Key,
                    loc => (Location)loc.Value.Deserialize());
            }


            base.Deserialize(newLocationComplex);

            return newLocationComplex;
        }

        public override void ResolveReferenceGuids()
        {
            if (newLocationComplex == null)
            {
                throw new NullReferenceException("Deserialize() has not been called before ResolveReferenceGuids().");
            }

            foreach (LocationSerializer locationSerializer in locations.Values)
            {
                locationSerializer.ResolveReferenceGuids();
            }

            // No references
        }
    }
}