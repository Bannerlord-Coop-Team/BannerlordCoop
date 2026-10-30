using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.External
{
    /// <summary>
    /// Binary package for Kingdom
    /// </summary>
    [Serializable]
    public class KingdomBinaryPackage : BinaryPackageBase<Kingdom>
    {

        private string stringId;
        private string[] clanIds;
        private string[] fiefIds;
        private string[] heroIds;
        private string[] deadLordIds;
        private string[] aliveLordIds;
        private string[] settlementIds;
        private string[] villageIds;
        private string[] factionsAtWarWithIds;
        private string[] alliedKingdomIds;

        public KingdomBinaryPackage(Kingdom obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        private static readonly HashSet<string> excludes = new HashSet<string>
        {
            "_distanceToClosestNonAllyFortificationCacheDirty",
            "_distanceToClosestNonAllyFortificationCache",
            "_clans",
            "_warPartyComponentsCache",
            "_deadLordsCache",
            "_aliveLordsCache",
            "_heroesCache",
            "_settlementsCache",
            "_villagesCache",
            "_fiefsCache",
            // By value these nest every warring kingdom inside the last one, past the 512 read depth with 40+ kingdoms
            "_factionsAtWarWith",
            "_alliedKingdoms",
        };

        protected override void PackInternal()
        {
            stringId = ResolveId(Object);

            base.PackFields(excludes);

            clanIds = ResolveIds(Object.Clans);
            fiefIds = ResolveIds(Object.Fiefs);
            heroIds = ResolveIds(Object.Heroes);
            deadLordIds = ResolveIds(Object.DeadLords);
            aliveLordIds = ResolveIds(Object.AliveLords);

            settlementIds = ResolveIds(Object.Settlements);
            villageIds = ResolveIds(Object.Villages);

            factionsAtWarWithIds = ResolveIds(Object._factionsAtWarWith?.OfType<MBObjectBase>());
            alliedKingdomIds = ResolveIds(Object._alliedKingdoms);
        }

        protected override void UnpackInternal()
        {
            var resolvedObj = ResolveObject<Kingdom>(stringId);
            if (resolvedObj != null)
            {
                Object = resolvedObj;
                return;
            }

            base.UnpackFields();

            Object.InitializeCachedLists();

            // Cached armies are handed in the ArmyBinaryPackage

            // Cached WarPartyComponents are handed in the
            // BanditComponentBinaryPackage and LordPartyComponentBinaryPackage

            Object._clans = ResolveObjects<Clan>(clanIds).ToMBList();
            Object._fiefsCache = ResolveObjects<Town>(fiefIds).ToMBList();
            Object._heroesCache = ResolveObjects<Hero>(heroIds).ToMBList();
            Object._aliveLordsCache = ResolveObjects<Hero>(aliveLordIds).ToMBList();
            Object._deadLordsCache = ResolveObjects<Hero>(deadLordIds).ToMBList();
            Object._settlementsCache = ResolveObjects<Settlement>(settlementIds).ToMBList();
            Object._villagesCache = ResolveObjects<Village>(villageIds).ToMBList();
            Object._factionsAtWarWith = ResolveObjects<MBObjectBase>(factionsAtWarWithIds).OfType<IFaction>().ToMBList();
            Object._alliedKingdoms = ResolveObjects<Kingdom>(alliedKingdomIds).ToMBList();
        }
    }
}
