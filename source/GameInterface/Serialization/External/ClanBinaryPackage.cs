using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class ClanBinaryPackage : BinaryPackageBase<Clan>
    {

        string stringId;

        string[] supporterNotablesIds;
        string[] deadLordsIds;
        string[] aliveLordsIds;
        string[] heroesIds;
        string[] companionsIds;


        public ClanBinaryPackage(Clan obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        public static HashSet<string> Excludes = new HashSet<string>
        {
            "_supporterNotablesCache",
            "_aliveLordsCache",
            "_deadLordsCache",
            "_heroesCache",
            "_companionsCache",
            "_defaultPartyTemplate",
        };

        protected override void PackInternal()
        {
            stringId = ResolveId(Object);

            base.PackFields(Excludes);
            
            supporterNotablesIds = ResolveIds(Object._supporterNotablesCache);
            deadLordsIds = ResolveIds(Object._deadLordsCache);
            aliveLordsIds = ResolveIds(Object._aliveLordsCache);
            heroesIds = ResolveIds(Object._heroesCache);
            companionsIds = ResolveIds(Object._companionsCache);
        }

        protected override void UnpackInternal()
        {
            var resolvedObj = ResolveObject<Clan>(stringId);
            if (resolvedObj != null)
            {
                Object = resolvedObj;
                return;
            }

            Object.InitMembers();

            base.UnpackFields();

            // Unpack special cases
            Object._supporterNotablesCache = ResolveObjects<Hero>(supporterNotablesIds).ToMBList();
            Object._deadLordsCache = ResolveObjects<Hero>(deadLordsIds).ToMBList();
            Object._aliveLordsCache = ResolveObjects<Hero>(aliveLordsIds).ToMBList();
            Object._heroesCache = ResolveObjects<Hero>(heroesIds).ToMBList();
            Object._companionsCache = ResolveObjects<Hero>(companionsIds).ToMBList();
        }
    }
}
