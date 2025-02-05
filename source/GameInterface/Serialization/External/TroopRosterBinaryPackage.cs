using Common.Extensions;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Library;

namespace GameInterface.Serialization.External
{
    /// <summary>
    /// Binary package for TroopRoster
    /// </summary>
    [Serializable]
    public class TroopRosterBinaryPackage : BinaryPackageBase<TroopRoster>
    {
        public TroopRosterBinaryPackage(TroopRoster obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        private static HashSet<string> excludes = new HashSet<string>
        {
            "_troopRosterElements",
            "_troopRosterElementsVersion",
            "<NumberChangedCallback>k__BackingField",
            "<VersionNo>k__BackingField",
        };

        protected override void PackInternal()
        {
            base.PackFields(excludes);
        }

        protected override void UnpackInternal()
        {
            base.UnpackFields();
            if (Object?.OwnerParty != null)
            {
                Object.NumberChangedCallback = Object.OwnerParty.MemberRosterNumberChanged;
            }

            Object._troopRosterElements = new MBList<TroopRosterElement>();
            Object.VersionNo = -1;
        }
    }
}
