using Common.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class ClanBinaryPackage : BinaryPackageBase<Clan>
    {
        public static readonly FieldInfo Clan_supporterNotablesCache = typeof(Clan).GetField("_supporterNotablesCache", BindingFlags.NonPublic | BindingFlags.Instance);
        public static readonly FieldInfo Clan_lordsCache = typeof(Clan).GetField("_lordsCache", BindingFlags.NonPublic | BindingFlags.Instance);
        public static readonly FieldInfo Clan_heroesCache = typeof(Clan).GetField("_heroesCache", BindingFlags.NonPublic | BindingFlags.Instance);
        public static readonly FieldInfo Clan_companionsCache = typeof(Clan).GetField("_companionsCache", BindingFlags.NonPublic | BindingFlags.Instance);

        string stringId;

        string[] supporterNotablesIds;
        string[] lordsIds;
        string[] heroesIds;
        string[] companionsIds;


        public ClanBinaryPackage(Clan obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        private static HashSet<string> excludes = new HashSet<string>
        {
            "_supporterNotablesCache",
            "_lordsCache",
            "_heroesCache",
            "_companionsCache",
        };

        protected override void PackInternal()
        {
            stringId = Object.StringId;

            foreach (FieldInfo field in ObjectType.GetAllInstanceFields())
            {
                object obj = field.GetValue(Object);
                StoredFields.Add(field, BinaryPackageFactory.GetBinaryPackage(obj));
            }

            supporterNotablesIds = PackIds((List<Hero>)Clan_supporterNotablesCache.GetValue(Object));
            lordsIds = PackIds((List<Hero>)Clan_lordsCache.GetValue(Object));
            heroesIds = PackIds((List<Hero>)Clan_heroesCache.GetValue(Object));
            companionsIds = PackIds((List<Hero>)Clan_companionsCache.GetValue(Object));
        }

        private static readonly MethodInfo Clan_InitMembers = typeof(Clan).GetMethod("InitMembers", BindingFlags.NonPublic | BindingFlags.Instance);
        protected override void UnpackInternal()
        {
            // If the stringId already exists in the object manager use that object
            if (stringId != null)
            {
                var newObject = MBObjectManager.Instance.GetObject<Clan>(stringId);
                if (newObject != null)
                {
                    Object = newObject;
                    return;
                }
            }

            Clan_InitMembers.Invoke(Object, new object[0]);

            TypedReference reference = __makeref(Object);
            foreach (FieldInfo field in StoredFields.Keys)
            {
                field.SetValueDirect(reference, StoredFields[field].Unpack());
            }

            // Unpack special cases
            Clan_supporterNotablesCache.SetValue(Object, ResolveIds<Hero>(supporterNotablesIds).ToMBList());
            Clan_lordsCache.SetValue(Object, ResolveIds<Hero>(lordsIds).ToMBList());
            Clan_heroesCache.SetValue(Object, ResolveIds<Hero>(heroesIds).ToMBList());
            Clan_companionsCache.SetValue(Object, ResolveIds<Hero>(companionsIds).ToMBList());
        }
    }
}
