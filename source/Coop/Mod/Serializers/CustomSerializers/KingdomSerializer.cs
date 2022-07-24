using Common;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Coop.Mod.Serializers.Custom
{
    [Serializable]
    public class KingdomSerializer : CustomSerializerWithGuid
    {
        [NonSerialized]
        Kingdom newKingdom;

        /// <summary>
        /// Serialized Natively Non Serializable Objects (SNNSO)
        /// </summary>
        readonly Dictionary<FieldInfo, ICustomSerializer> SNNSO = new Dictionary<FieldInfo, ICustomSerializer>();
        readonly Dictionary<FieldInfo, Guid> references = new Dictionary<FieldInfo, Guid>();

        public KingdomSerializer(Kingdom kingdom) : base(kingdom)
        {
            List<string> UnmanagedFields = new List<string>();

            foreach (FieldInfo fieldInfo in NonSerializableObjects)
            {
                // Get value from fieldInfo
                object value = fieldInfo.GetValue(kingdom);

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
                    case "<Name>k__BackingField":
                    case "<InformalName>k__BackingField":
                    case "<EncyclopediaText>k__BackingField":
                    case "<EncyclopediaTitle>k__BackingField":
                    case "<EncyclopediaRulerTitle>k__BackingField":
                        SNNSO.Add(fieldInfo, new TextObjectSerializer((TextObject)value));
                        break;
                    case "<Banner>k__BackingField":
                        SNNSO.Add(fieldInfo, new BannerSerializer((Banner)value));
                        break;
                    case "<LastKingdomDecisionConclusionDate>k__BackingField":
                    case "<LastMercenaryOfferTime>k__BackingField":
                    case "<NotAttackableByPlayerUntilTime>k__BackingField":
                        SNNSO.Add(fieldInfo, new CampaignTimeSerializer((CampaignTime)value));
                        break;
                    case "<Culture>k__BackingField":
                        // TODO
                        break;
                    case "<InitialHomeLand>k__BackingField":
                    case "_rulingClan":
                        references.Add(fieldInfo, CoopObjectManager.GetGuid(value));
                        break;
                    default:
                        UnmanagedFields.Add(fieldInfo.Name);
                        break;
                }
            }

            foreach (FieldInfo fieldInfo in NonSerializableCollections)
            {
                // Get value from fieldInfo
                object value = fieldInfo.GetValue(kingdom);

                // If value is null, no need to serialize
                if (value == null)
                {
                    continue;
                }

                // Assign serializer to nonserializable objects
                switch (fieldInfo.Name)
                {
                    case "_unresolvedDecisions":
                    case "_stances":
                    case "_clans":
                    case "_armies":
                    case "_activePolicies":
                    case "Provocations":
                        //SNNSO.Add(fieldInfo, new KingdomDecisionSerialzer((KingdomDecision)value));
                        // TODO fix later
                        break;

                    case "_fiefsCache":
                    case "_fiefsReadOnlyCache":
                    case "_villagesCache":
                    case "_villagesReadOnlyCache":
                    case "_settlementsCache":
                    case "_settlementsReadOnlyCache":
                    case "_heroesCache":
                    case "_heroesReadOnlyCache":
                    case "_lordsCache":
                    case "_warPartyComponentsCache":
                    case "_warPartiesReadOnlyCache":
                    case "_lordsReadOnlyCache":
                        // Caches are skipped in serialization
                        break;

                    case "<Armies>k__BackingField":
                        // Getter setter
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
            newKingdom = new Kingdom();

            // Objects requiring a custom serializer
            foreach (KeyValuePair<FieldInfo, ICustomSerializer> entry in SNNSO)
            {
                entry.Key.SetValue(newKingdom, entry.Value.Deserialize());
            }

            foreach (KeyValuePair<FieldInfo, Guid> entry in references)
            {
                FieldInfo field = entry.Key;
                Guid id = entry.Value;

                field.SetValue(newKingdom, CoopObjectManager.GetObject(id));
            }

            return base.Deserialize(newKingdom);
        }

        public override void ResolveReferenceGuids()
        {
            // TODO
        }
    }
}