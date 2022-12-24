using Common.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace GameInterface.Serialization.Impl
{
    [Serializable]
    public class ClanBinaryPackage : BinaryPackageBase<Clan>
    {
        public ClanBinaryPackage(Clan obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        private static HashSet<string> excludes = new HashSet<string>
        {
            "_supporterNotablesCache",
            "_fiefsCache",
            "_villagesCache",
            "_settlementsCache",
            "_lordsCache",
            "_heroesCache",
            "_companionsCache",
            "_warPartyComponentsCache",
            "_clanMidSettlement",
            "_stances",
            "_distanceToClosestNonAllyFortificationCache",
            "_distanceToClosestNonAllyFortificationCacheDirty",
            "TotalStrength",
            "_midPointCalculated",
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
