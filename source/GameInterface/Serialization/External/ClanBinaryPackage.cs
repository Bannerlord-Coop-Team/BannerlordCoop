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
            stringId = Object.StringId;

            base.PackFields();
            
            supporterNotablesIds = PackIds(Object._supporterNotablesCache);
            lordsIds = PackIds(Object._lordsCache);
            heroesIds = PackIds(Object._heroesCache);
            companionsIds = PackIds(Object._companionsCache);
        }

        protected override void UnpackInternal()
        {
            // If the stringId already exists in the object manager use that object
            if (stringId != null)
            {
                var newObject = ResolveId<Clan>(stringId);
                if (newObject != null)
                {
                    Object = newObject;
                    return;
                }
            }

            Object.InitMembers();

            base.UnpackFields();

            // Unpack special cases
            Object._supporterNotablesCache = ResolveIds<Hero>(supporterNotablesIds).ToMBList();
            Object._lordsCache = ResolveIds<Hero>(lordsIds).ToMBList();
            Object._heroesCache = ResolveIds<Hero>(heroesIds).ToMBList();
            Object._companionsCache = ResolveIds<Hero>(companionsIds).ToMBList();
        }
    }
}
