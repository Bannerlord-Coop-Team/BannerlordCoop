using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using static TaleWorlds.CampaignSystem.Village;

namespace Coop.Mod.Serializers.Custom
{
    [Serializable]
    public class VillageSerializer : CustomSerializer
    {
        [NonSerialized]
        Village newvillage;

        Guid bound;
        VillageStates state;

        /// <summary>
        /// Serialized Natively Non Serializable Objects (SNNSO)
        /// </summary>
        Dictionary<FieldInfo, ICustomSerializer> SNNSO = new Dictionary<FieldInfo, ICustomSerializer>();

        public VillageSerializer(Village village) : base(village)
        {
            List<string> UnmanagedFields = new List<string>();

            foreach (FieldInfo fieldInfo in NonSerializableObjects)
            {
                // Get value from fieldInfo
                object value = fieldInfo.GetValue(village);

                // If value is null, no need to serialize
                if (value == null)
                {
                    continue;
                }

                // Assign serializer to nonserializable objects
                switch (fieldInfo.Name)
                {
                    case "_bound":
                        bound = CoopObjectManager.GetGuid((Settlement)value);
                        break;
                    case "VillageType":
                        SNNSO.Add(fieldInfo, new VillageTypeSerializer((VillageType)value));
                        break;
                    case "VillagerPartyComponent":
                        SNNSO.Add(fieldInfo, new PartyComponentSerializer((PartyComponent)value));
                        break;
                    case "_villageState":
                        state = (VillageStates)value;
                        break;
                    case "_marketData":
                        SNNSO.Add(fieldInfo, new VillageMarketDataSerializer((VillageMarketData)value));
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

            // TODO manage collections

            // Remove non serializable objects before serialization
            // They are marked as nonserializable in CustomSerializer but still tries to serialize???
            NonSerializableCollections.Clear();
            NonSerializableObjects.Clear();

        }

        public override object Deserialize()
        {
            //It calls the base constructor so it creates the VillagemarketData object in it in theory
            newvillage = MBObjectManager.Instance.CreateObject<Village>();

            foreach (KeyValuePair<FieldInfo, ICustomSerializer> entry in SNNSO)
            { 
                entry.Key.SetValue(newvillage, entry.Value.Deserialize());
            }

            newvillage.GetType()
                .GetField("_villageState", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(newvillage, state);

            base.Deserialize(newvillage);

            return newvillage;
        }


        public override void ResolveReferenceGuids()
        {
            if (newvillage == null)
            {
                throw new NullReferenceException("Deserialize() has not been called before ResolveReferenceGuids().");
            }

            foreach (KeyValuePair<FieldInfo, ICustomSerializer> entry in SNNSO)
            {
                entry.Value.ResolveReferenceGuids();
            }

            newvillage.GetType()
                .GetField("_bound", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(newvillage, CoopObjectManager.GetObject(bound));
        }
    }
}