using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using static TaleWorlds.CampaignSystem.Settlement;

namespace Coop.Mod.Serializers.Custom
{
    [Serializable]
    public class SettlementSerializer : CustomSerializerWithGuid
    {
        [NonSerialized]
        Settlement newSettlement;

        /// <summary>
        /// Serialized Natively Non Serializable Objects (SNNSO)
        /// </summary>
        readonly Dictionary<FieldInfo, ICustomSerializer> SNNSO = new Dictionary<FieldInfo, ICustomSerializer>();
        readonly Dictionary<FieldInfo, Guid> references = new Dictionary<FieldInfo, Guid>();
        readonly Dictionary<FieldInfo, Guid[]> arrayOfReferences = new Dictionary<FieldInfo, Guid[]>();

        readonly FieldInfo settlementComponentsFieldInfo;
        readonly FieldInfo valueForFactionFieldInfo;
        readonly Tuple<Guid, float>[] valueForFaction;
        public SettlementSerializer(Settlement settlement) : base(settlement)
        {
            List<FieldInfo> UnmanagedFields = new List<FieldInfo>();

            foreach (FieldInfo fieldInfo in NonSerializableObjects)
            {
                // Get value from fieldInfo
                object value = fieldInfo.GetValue(settlement);

                // If value is null, no need to serialize
                if (value == null)
                {
                    continue;
                }

                // Assign serializer to nonserializable objects
                switch (fieldInfo.Name)
                {
                    case "_name":
                    case "<EncyclopediaText>k__BackingField":
                        SNNSO.Add(fieldInfo, new TextObjectSerializer((TextObject)value));
                        break;
                    case "Town":
                        SNNSO.Add(fieldInfo, new TownSerializer((Town)value));
                        break;
                    case "Village":
                        SNNSO.Add(fieldInfo, new VillageSerializer((Village)value));
                        break;
                    case "Hideout":
                        SNNSO.Add(fieldInfo, new HideoutSerializer((Hideout)value));
                        break;
                    case "SiegeLanes":
                        SNNSO.Add(fieldInfo, new SiegeLaneSerializer((SiegeLane)value));
                        break;
                    case "Culture":
                        SNNSO.Add(fieldInfo, new CultureObjectSerializer((CultureObject)value));
                        break;
                    case "Stash":
                        SNNSO.Add(fieldInfo, new ItemRosterSerializer((ItemRoster)value));
                        break;
                    case "_valueForFaction":
                        Dictionary<IFaction, float> _valueForFactions = (Dictionary<IFaction, float>)value;
                        valueForFaction = _valueForFactions
                            .Select(factionValue => new Tuple<Guid, float>(
                                CoopObjectManager.GetGuid(factionValue.Key), factionValue.Value)
                            )
                            .ToArray();
                        valueForFactionFieldInfo = fieldInfo;
                        break;
                    case "_nextLocatable":
                        // TODO
                        break;
                    
                    case "_siegeEngineMissiles":
                        // TODO
                        break;
                    case "<Party>k__BackingField":
                        SNNSO.Add(fieldInfo, new PartyBaseSerializer((PartyBase)value));
                        break;
                    case "<LocationComplex>k__BackingField":
                        SNNSO.Add(fieldInfo, new LocationComplexSerializer((LocationComplex)value));
                        break;
                    case "MilitiaPartyComponent":
                        SNNSO.Add(fieldInfo, new PartyComponentSerializer((PartyComponent)value));
                        break;
                    case "<CurrentNavigationFace>k__BackingField":
                        SNNSO.Add(fieldInfo, new PathFaceRecordSerializer((PathFaceRecord)value));
                        break;


                    case "_boundVillages":
                        List<Village> villages = (List<Village>)value;
                        Guid[] boundVillages = villages.Select(village => CoopObjectManager.GetGuid(village)).ToArray();
                        arrayOfReferences.Add(fieldInfo, boundVillages);
                        break;
                    // Caches are needed as they hold existing companions and parties in settlement
                    case "_partiesCache":
                        List<MobileParty> _partiesCache = (List<MobileParty>)value;
                        Guid[] partiesCache = _partiesCache.Select(party => CoopObjectManager.GetGuid(party)).ToArray();
                        arrayOfReferences.Add(fieldInfo, partiesCache);
                        break;
                    case "_heroesWithoutPartyCache":
                        List<Hero> _heroesWithoutPartyCache = (List<Hero>)value;
                        Guid[] herosWithoutParty = _heroesWithoutPartyCache.Select(hero => CoopObjectManager.GetGuid(hero)).ToArray();
                        arrayOfReferences.Add(fieldInfo, herosWithoutParty);
                        break;
                    case "_notablesCache":
                        List<Hero> _notablesCache = (List<Hero>)value;
                        Guid[] notablesCache = _notablesCache.Select(hero => CoopObjectManager.GetGuid(hero)).ToArray();
                        arrayOfReferences.Add(fieldInfo, notablesCache);
                        break;

                    

                    case "_settlementComponents":
                        settlementComponentsFieldInfo = fieldInfo;
                        break;
                    // References
                    case "_ownerClan":
                    case "ClaimedBy":
                    case "_lastAttackerParty":
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

            // Remove non serializable objects before serialization
            // They are marked as nonserializable in CustomSerializer but still tries to serialize???
            NonSerializableCollections.Clear();
            NonSerializableObjects.Clear();
        }

        public override object Deserialize()
        {
            newSettlement = MBObjectManager.Instance.CreateObject<Settlement>();

            foreach (KeyValuePair<FieldInfo, ICustomSerializer> entry in SNNSO)
            {
                entry.Key.SetValue(newSettlement, entry.Value.Deserialize());
            }

            return newSettlement;
        }

        public override void ResolveReferenceGuids()
        {
            if (newSettlement == null)
            {
                throw new NullReferenceException("Deserialize() has not been called before ResolveReferenceGuids().");
            }

            foreach(KeyValuePair<FieldInfo, Guid[]> fieldArray in arrayOfReferences)
            {
                FieldInfo field = fieldArray.Key;
                Guid[] guids = fieldArray.Value;
                List<object> precast = guids.Select(item => CoopObjectManager.GetObject(item)).ToList();

                // This is insane
                object fieldList = Activator.CreateInstance(field.FieldType);
                MethodInfo listAdd = field.FieldType.GetMethod("Add");

                foreach(object item in precast)
                {
                    listAdd.Invoke(fieldList, new object[] { item });
                }

                field.SetValue(newSettlement, fieldList);
            }

            foreach (KeyValuePair<FieldInfo, Guid> entry in references)
            {
                FieldInfo field = entry.Key;
                Guid id = entry.Value;

                field.SetValue(newSettlement, CoopObjectManager.GetObject(id));
            }

            List<SettlementComponent> settlementComponents = (List<SettlementComponent>)settlementComponentsFieldInfo.GetValue(newSettlement);
            if (newSettlement.Village != null)
            {
                settlementComponents.Add(newSettlement.Village);
            }
            if(newSettlement.Town != null)
            {
                settlementComponents.Add(newSettlement.Town);
            }
            if(newSettlement.Hideout != null)
            {
                settlementComponents.Add(newSettlement.Hideout);
            }
        }
    }
}