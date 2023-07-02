using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class ItemRosterBinaryPackage : BinaryPackageBase<ItemRoster>
    {
        public ItemRosterBinaryPackage(ItemRoster obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        static readonly HashSet<string> excludes = new HashSet<string>
        {
            "<TotalWeight>k__BackingField",
            "<VersionNo>k__BackingField",
            "<TotalValue>k__BackingField",
            "<TotalFood>k__BackingField",
            "<NumberOfPackAnimals>k__BackingField",
            "<NumberOfMounts>k__BackingField",
            "<NumberOfLivestockAnimals>k__BackingField",
            "<FoodVariety>k__BackingField",
            "_rosterUpdatedEvent"
        };

        protected override void PackInternal()
        {
            base.PackFields(excludes);
        }

        protected override void UnpackInternal()
        {
            base.UnpackFields();
        }
    }
}
