using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using static TaleWorlds.CampaignSystem.Town;

namespace Coop.Mod.Serializers.Custom
{
    [Serializable]
    public class TownSerializer : CustomSerializerWithGuid
    {
        [NonSerialized]
        Town town;

        /// <summary>
        /// Serialized Natively Non Serializable Objects (SNNSO)
        /// </summary>
        readonly Dictionary<FieldInfo, ICustomSerializer> SNNSO = new Dictionary<FieldInfo, ICustomSerializer>();
        readonly Dictionary<FieldInfo, Guid> references = new Dictionary<FieldInfo, Guid>();
        readonly Dictionary<FieldInfo, Guid[]> arrayOfReferences = new Dictionary<FieldInfo, Guid[]>();

        readonly FieldInfo buildingsField;
        readonly BuildingsSerializer[] buildingsSerializers = new BuildingsSerializer[0];

        readonly FieldInfo workshopsField;
        readonly WorkshopSerializer[] workshopSerializers = new WorkshopSerializer[0];
        public TownSerializer(Town town) : base(town)
        {
            List<FieldInfo> UnmanagedFields = new List<FieldInfo>();

            foreach (FieldInfo fieldInfo in NonSerializableObjects)
            {
                // Get value from fieldInfo
                object value = fieldInfo.GetValue(town);

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
                    case "_marketData":
                        SNNSO.Add(fieldInfo, new TownMarketDataSerializer((TownMarketData)value));
                        break;
                    case "_soldItems":
                        SNNSO.Add(fieldInfo, new SellLogSerializer((SellLog)value));
                        break;
                    case "GarrisonPartyComponent":
                        SNNSO.Add(fieldInfo, new PartyComponentSerializer((PartyComponent)value));
                        break;

                    // Caches are needed as they hold existing companions and parties in settlement
                    case "Buildings":
                        buildingsField = fieldInfo;
                        List<Building> Buildings = (List<Building>)value;
                        buildingsSerializers = Buildings.Select(building => new BuildingsSerializer(building)).ToArray();
                        break;
                    case "BuildingsInProgress":
                        Queue<Building> BuildingsInProgress = (Queue<Building>)value;
                        Guid[] buildingsInProgress = BuildingsInProgress
                            .Select(building => CoopObjectManager.AddObject(building)).ToArray();
                        arrayOfReferences.Add(fieldInfo, buildingsInProgress);
                        break;
                    case "Workshops":
                        workshopsField = fieldInfo;
                        Workshop[] Workshops = (Workshop[])value;
                        workshopSerializers = Workshops.Select(workshop => new WorkshopSerializer(workshop)).ToArray();
                        break;

                    // References
                    case "LastCapturedBy":
                    case "_ownerClan":
                    case "_governor":
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
            town = new Town();

            foreach (KeyValuePair<FieldInfo, ICustomSerializer> entry in SNNSO)
            {
                entry.Key.SetValue(town, entry.Value.Deserialize());
            }

            if(buildingsField != null)
            {
                List<Building> Buildings = buildingsSerializers.Select(building => (Building)building.Deserialize()).ToList();
                buildingsField.SetValue(town, Buildings);
            }
            if(workshopsField != null)
            {
                Workshop[] Workshops = workshopSerializers.Select(workshop => (Workshop)workshop.Deserialize()).ToArray();
                workshopsField.SetValue(town, Workshops);
            }


            base.Deserialize(town);

            return town;
        }

        public override void ResolveReferenceGuids()
        {
            if (town == null)
            {
                throw new NullReferenceException("Deserialize() has not been called before ResolveReferenceGuids().");
            }

            foreach (KeyValuePair<FieldInfo, Guid[]> fieldArray in arrayOfReferences)
            {
                FieldInfo field = fieldArray.Key;
                Guid[] guids = fieldArray.Value;
                List<object> precast = guids.Select(item => CoopObjectManager.GetObject(item)).ToList();

                // This is insane
                object fieldList = Activator.CreateInstance(field.FieldType);
                MethodInfo listAdd = field.FieldType.GetMethod("Add");

                foreach (object item in precast)
                {
                    listAdd.Invoke(fieldList, new object[] { item });
                }

                field.SetValue(town, fieldList);
            }


            foreach (KeyValuePair<FieldInfo, Guid> entry in references)
            {
                FieldInfo field = entry.Key;
                Guid id = entry.Value;

                field.SetValue(town, CoopObjectManager.GetObject(id));
            }
        }
    }
}