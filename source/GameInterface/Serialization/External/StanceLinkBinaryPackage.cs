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
        public static readonly PropertyInfo StanceLink_Faction1 = typeof(StanceLink).GetProperty(nameof(StanceLink.Faction1));
        public static readonly PropertyInfo StanceLink_Faction2 = typeof(StanceLink).GetProperty(nameof(StanceLink.Faction2));

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

            StanceLink_Faction1.SetValue(Object, ResolveId<Clan>(faction1Id));
            StanceLink_Faction2.SetValue(Object, ResolveId<Clan>(faction2Id));
        }
    }
}
