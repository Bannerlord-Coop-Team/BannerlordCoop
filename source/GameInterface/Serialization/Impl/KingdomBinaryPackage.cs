using Common.Extensions;
using System;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Serialization.Impl
{
    /// <summary>
    /// Binary package for Kingdom
    /// </summary>
    [Serializable]
    public class KingdomBinaryPackage : BinaryPackageBase<Kingdom>
    {
        public KingdomBinaryPackage(Kingdom obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        private static HashSet<string> excludes = new HashSet<string>
        {
            "_midPointCalculated",
            "_kingdomMidSettlement",
            "_distanceToClosestNonAllyFortificationCacheDirty",
            "_distanceToClosestNonAllyFortificationCache",
            "_clans",
            "_warPartyComponentsCache",
            "_lordsCache",
            "_heroesCache",
            "_settlementsCache",
            "_villagesCache",
            "_fiefsCache",
            "_stances",
        };

        public override void Pack()
        {
            foreach (FieldInfo field in ObjectType.GetAllInstanceFields())
            {
                object obj = field.GetValue(Object);
                StoredFields.Add(field, BinaryPackageFactory.GetBinaryPackage(obj));
            }
        }

        protected override void UnpackInternal()
        {
            TypedReference reference = __makeref(Object);
            foreach (FieldInfo field in StoredFields.Keys)
            {
                field.SetValueDirect(reference, StoredFields[field].Unpack());
            }
        }
    }
}
