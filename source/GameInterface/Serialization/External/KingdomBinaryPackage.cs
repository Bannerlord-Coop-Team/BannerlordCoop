using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

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
        private string[] lordIds;
        private string[] settlementIds;
        private string[] villageIds;

        public KingdomBinaryPackage(Kingdom obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        private static readonly HashSet<string> excludes = new HashSet<string>
        {
            "_distanceToClosestNonAllyFortificationCacheDirty",
            "_distanceToClosestNonAllyFortificationCache",
            "_clans",
            "_warPartyComponentsCache",
            "_lordsCache",
            "_heroesCache",
            "_settlementsCache",
            "_villagesCache",
            "_fiefsCache",
        };

        protected override void PackInternal()
        {
            stringId = Object.StringId;

            base.PackFields(excludes);

            clanIds = PackIds(Object.Clans);
            fiefIds = PackIds(Object.Fiefs);
            heroIds = PackIds(Object.Heroes);
            lordIds = PackIds(Object.Lords);
            settlementIds = PackIds(Object.Settlements);
            villageIds = PackIds(Object.Villages);
        }

        //public static readonly MethodInfo InitializeCachedLists = typeof(Kingdom).GetMethod("InitializeCachedLists", BindingFlags.NonPublic | BindingFlags.Instance);
        protected override void UnpackInternal()
        {
            if(stringId != null)
            {
                Kingdom kingdom = ResolveId<Kingdom>(stringId);
                if (kingdom != null)
                {
                    Object = kingdom;
                    return;
                }
            }

            base.UnpackFields();

            Object.InitializeCachedLists();

            // Cached armies are handed in the ArmyBinaryPackage

            // Cached WarPartyComponents are handed in the
            // BanditComponentBinaryPackage and LordPartyComponentBinaryPackage

            Object._clans = ResolveIds<Clan>(clanIds).ToMBList();
            Object._fiefsCache = ResolveIds<Town>(fiefIds).ToMBList();
            Object._heroesCache = ResolveIds<Hero>(heroIds).ToMBList();
            Object._lordsCache = ResolveIds<Hero>(lordIds).ToMBList();
            Object._settlementsCache = ResolveIds<Settlement>(settlementIds).ToMBList();
            Object._villagesCache = ResolveIds<Village>(villageIds).ToMBList();
        }
    }
}
