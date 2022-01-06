using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Coop.Mod.Serializers.Custom
{
    public class KingdomSerializer : CustomSerializerWithGuid
    {
        [NonSerialized]
        Kingdom newKingdom;

        /// <summary>
        /// Serialized Natively Non Serializable Objects (SNNSO)
        /// </summary>
        readonly Dictionary<FieldInfo, ICustomSerializer> SNNSO = new Dictionary<FieldInfo, ICustomSerializer>();
        readonly Dictionary<FieldInfo, Guid> references = new Dictionary<FieldInfo, Guid>();

        //      [SaveableField(10)]
        //private List<KingdomDecision> _unresolvedDecisions = new List<KingdomDecision>();

        //// Token: 0x04000431 RID: 1073
        //[CachedData]
        //private List<StanceLink> _stances = new List<StanceLink>();

        //// Token: 0x04000432 RID: 1074
        //[CachedData]
        //private List<Town> _fiefsCache;

        //// Token: 0x04000433 RID: 1075
        //[CachedData]
        //private MBReadOnlyList<Town> _fiefsReadOnlyCache;

        //// Token: 0x04000434 RID: 1076
        //[CachedData]
        //private List<Village> _villagesCache;

        //// Token: 0x04000435 RID: 1077
        //[CachedData]
        //private MBReadOnlyList<Village> _villagesReadOnlyCache;

        //// Token: 0x04000436 RID: 1078
        //[CachedData]
        //private List<Settlement> _settlementsCache;

        //// Token: 0x04000437 RID: 1079
        //[CachedData]
        //private MBReadOnlyList<Settlement> _settlementsReadOnlyCache;

        //// Token: 0x04000439 RID: 1081
        //[CachedData]
        //private List<Hero> _heroesCache;

        //// Token: 0x0400043A RID: 1082
        //[CachedData]
        //private MBReadOnlyList<Hero> _heroesReadOnlyCache;

        //// Token: 0x0400043B RID: 1083
        //[CachedData]
        //private List<Hero> _lordsCache;

        //// Token: 0x0400043C RID: 1084
        //[CachedData]
        //private List<WarPartyComponent> _warPartyComponentsCache;

        //// Token: 0x0400043D RID: 1085
        //[CachedData]
        //private MBReadOnlyList<WarPartyComponent> _warPartiesReadOnlyCache;

        //// Token: 0x0400043E RID: 1086
        //[CachedData]
        //private MBReadOnlyList<Hero> _lordsReadOnlyCache;

        //// Token: 0x04000440 RID: 1088
        //[CachedData]
        //private List<Clan> _clans;

        //// Token: 0x04000443 RID: 1091
        //[SaveableField(20)]
        //private readonly List<Army> _armies;

        //// Token: 0x04000448 RID: 1096
        //[SaveableField(26)]
        //private List<PolicyObject> _activePolicies;

        //// Token: 0x04000449 RID: 1097
        //[SaveableField(27)]
        //public List<Kingdom.Provocation> Provocations;

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
                    case "<Name>k__BackingField":
                    case "<InformalName>k__BackingField":
                    case "<EncyclopediaText>k__BackingField":
                        SNNSO.Add(fieldInfo, new TextObjectSerializer((TextObject)value));
                        break;
                    case "_rulingClan":
                        references.Add(fieldInfo, CoopObjectManager.GetGuid((Clan)value));
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
                    default:
                        UnmanagedFields.Add(fieldInfo.Name);
                        break;
                }

                if (!UnmanagedFields.IsEmpty())
                {
                    throw new NotImplementedException($"Cannot serialize {UnmanagedFields}");
                }
            }
        }

        public override object Deserialize()
        {
            newKingdom = new Kingdom();
            // TODO
            return base.Deserialize(newKingdom);
        }

        public override void ResolveReferenceGuids()
        {
            // TODO
        }
    }
}