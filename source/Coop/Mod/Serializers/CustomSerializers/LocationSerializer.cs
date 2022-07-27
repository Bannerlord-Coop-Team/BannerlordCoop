using Common;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Coop.Mod.Serializers.Custom
{
    [Serializable]
    public class LocationSerializer : ICustomSerializer
    {
        [NonSerialized]
        private Location newLocation;

        string stringId;
        TextObjectSerializer name;
        TextObjectSerializer doorName;
        int prosperityMax;
        bool isIndoor;
        bool canBeReserved;
        string playerCanEnter;
        string playerCanSee;
        string aiCanExit;
        string aiCanEnter;
        string[] sceneNames;

        FieldInfo locationComplexFieldInfo;
        Guid locationComplex;

        public LocationSerializer(Location location)
        {
            List<FieldInfo> UnmanagedFields = new List<FieldInfo>();

            FieldInfo[] fields = typeof(Location).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (FieldInfo fieldInfo in fields)
            {
                // Get value from fieldInfo
                object value = fieldInfo.GetValue(location);

                // If value is null, no need to serialize
                if (value == null)
                {
                    continue;
                }

                // Assign serializer to nonserializable objects
                switch (fieldInfo.Name)
                {
                    case "ProsperityMax":
                        prosperityMax = (int)value;
                        break;
                    case "<StringId>k__BackingField":
                        stringId = (string)value;
                        break;
                    case "<IsIndoor>k__BackingField":
                        isIndoor = (bool)value;
                        break;
                    case "_name":
                        name = new TextObjectSerializer((TextObject)value);
                        break;
                    case "_doorName":
                        doorName = new TextObjectSerializer((TextObject)value);
                        break;
                    case "<CanBeReserved>k__BackingField":
                        canBeReserved = (bool)value;
                        break;
                    case "_aiCanEnter":
                        aiCanEnter = (string)value;
                        break;
                    case "_playerCanEnter":
                        playerCanEnter = (string)value;
                        break;
                    case "_playerCanSee":
                        playerCanSee = (string)value;
                        break;
                    case "_aiCanExit":
                        aiCanExit = (string)value;
                        break;
                    case "_ownerComplex":
                        locationComplexFieldInfo = fieldInfo;
                        locationComplex = CoopObjectManager.GetGuid(value);
                        break;
                    case "_sceneNames":
                        sceneNames = (string[])value;
                        break;

                    case "_characterList":
                    case "<LocationsOfPassages>k__BackingField":
                    case "<SpecialItems>k__BackingField":
                    case "<IsReserved>k__BackingField":
                    case "_overriddenName":
                    case "_overriddenDoorName":
                        // Do nothing
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

        public object Deserialize()
        {
            newLocation = new Location(
                stringId,
                (TextObject)name.Deserialize(),
                (TextObject)doorName.Deserialize(),
                prosperityMax,
                isIndoor,
                canBeReserved,
                playerCanEnter,
                playerCanSee,
                aiCanExit,
                aiCanEnter,
                sceneNames,
                null);

            return newLocation;
        }

        public T Deserialize<T>()
        {
            return (T)Deserialize();
        }

        public void ResolveReferenceGuids()
        {
            if (locationComplexFieldInfo != null)
            {
                locationComplexFieldInfo.SetValue(newLocation, CoopObjectManager.GetObject(locationComplex));
            }
        }
    }
}