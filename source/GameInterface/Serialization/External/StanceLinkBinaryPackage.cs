using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Serialization.External
{
    /// <summary>
    /// Binary package for StanceLink
    /// </summary>
    [Serializable]
    public class StanceLinkBinaryPackage : BinaryPackageBase<StanceLink>
    {

        private string faction1Id;
        private string faction2Id;
        public StanceLinkBinaryPackage(StanceLink obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        private static readonly HashSet<string> excludes = new HashSet<string>
        {
            "<Faction1>k__BackingField",
            "<Faction2>k__BackingField",
        };

        protected override void PackInternal()
        {
            base.PackFields(excludes);

            faction1Id = Object.Faction1?.StringId;
            faction2Id = Object.Faction2?.StringId;
        }

        protected override void UnpackInternal()
        {
            base.UnpackFields();
            Object.Faction1 = ResolveId<Clan>(faction1Id);
            Object.Faction2 = ResolveId<Clan>(faction2Id);
        }
    }
}
