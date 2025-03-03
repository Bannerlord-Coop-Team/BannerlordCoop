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

            clanIds = ResolveIds(Object.Clans);
            fiefIds = ResolveIds(Object.Fiefs);
            heroIds = ResolveIds(Object.Heroes);
            lordIds = ResolveIds(Object.Lords);
            settlementIds = ResolveIds(Object.Settlements);
            villageIds = ResolveIds(Object.Villages);
        }

        protected override void UnpackInternal()
        {
            if(stringId != null)
            {
                Kingdom kingdom = ResolveObject<Kingdom>(stringId);
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

            Object._clans = ResolveObjects<Clan>(clanIds).ToMBList();
            Object._fiefsCache = ResolveObjects<Town>(fiefIds).ToMBList();
            Object._heroesCache = ResolveObjects<Hero>(heroIds).ToMBList();
            Object._lordsCache = ResolveObjects<Hero>(lordIds).ToMBList();
            Object._settlementsCache = ResolveObjects<Settlement>(settlementIds).ToMBList();
            Object._villagesCache = ResolveObjects<Village>(villageIds).ToMBList();
        }
    }
}
