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
        string[] lordsIds;
        string[] heroesIds;
        string[] companionsIds;


        public ClanBinaryPackage(Clan obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
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
            stringId = ResolveId(Object);

            base.PackFields();
            
            supporterNotablesIds = ResolveIds(Object._supporterNotablesCache);
            lordsIds = ResolveIds(Object._lordsCache);
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
            Object._lordsCache = ResolveObjects<Hero>(lordsIds).ToMBList();
            Object._heroesCache = ResolveObjects<Hero>(heroesIds).ToMBList();
            Object._companionsCache = ResolveObjects<Hero>(companionsIds).ToMBList();
        }
    }
}
