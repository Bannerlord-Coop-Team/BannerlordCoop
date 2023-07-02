﻿using System;
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
        public static readonly FieldInfo Kingdom_Armies = typeof(Kingdom).GetField("_armies", BindingFlags.NonPublic | BindingFlags.Instance);
        public static readonly FieldInfo Kingdom_Clans = typeof(Kingdom).GetField("_clans", BindingFlags.NonPublic | BindingFlags.Instance);
        public static readonly FieldInfo Kingdom_Fiefs = typeof(Kingdom).GetField("_fiefsCache", BindingFlags.NonPublic | BindingFlags.Instance);
        public static readonly FieldInfo Kingdom_Heroes = typeof(Kingdom).GetField("_heroesCache", BindingFlags.NonPublic | BindingFlags.Instance);
        public static readonly FieldInfo Kingdom_Lords = typeof(Kingdom).GetField("_lordsCache", BindingFlags.NonPublic | BindingFlags.Instance);
        public static readonly FieldInfo Kingdom_Settlements = typeof(Kingdom).GetField("_settlementsCache", BindingFlags.NonPublic | BindingFlags.Instance);
        public static readonly FieldInfo Kingdom_Villages = typeof(Kingdom).GetField("_villagesCache", BindingFlags.NonPublic | BindingFlags.Instance);
        public static readonly FieldInfo Kingdom_WarPartyComponents = typeof(Kingdom).GetField("_warPartyComponentsCache", BindingFlags.NonPublic | BindingFlags.Instance);

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

        public static readonly MethodInfo InitializeCachedLists = typeof(Kingdom).GetMethod("InitializeCachedLists", BindingFlags.NonPublic | BindingFlags.Instance);
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

            InitializeCachedLists.Invoke(Object, Array.Empty<object>());

            // Cached armies are handed in the ArmyBinaryPackage

            // Cached WarPartyComponents are handed in the
            // BanditComponentBinaryPackage and LordPartyComponentBinaryPackage

            Kingdom_Clans.SetValue(Object, ResolveIds<Clan>(clanIds).ToMBList());
            Kingdom_Fiefs.SetValue(Object, ResolveIds<Town>(fiefIds).ToMBList());
            Kingdom_Heroes.SetValue(Object, ResolveIds<Hero>(heroIds).ToMBList());
            Kingdom_Lords.SetValue(Object, ResolveIds<Hero>(lordIds).ToMBList());
            Kingdom_Settlements.SetValue(Object, ResolveIds<Settlement>(settlementIds).ToMBList());
            Kingdom_Villages.SetValue(Object, ResolveIds<Village>(villageIds).ToMBList());
        }
    }
}
