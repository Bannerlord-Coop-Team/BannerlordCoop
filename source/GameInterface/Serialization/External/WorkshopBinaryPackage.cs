using System;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.External
{
    /// <summary>
    /// Binary package for Workshop
    /// </summary>
    [Serializable]
    public class WorkshopBinaryPackage : BinaryPackageBase<Workshop>
    {
        string townId;
        int workshopIndex;

        public WorkshopBinaryPackage(Workshop obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        protected override void PackInternal()
        {
            Town town = Object.Settlement.Town;
            townId = town.StringId;
            if (townId == null) throw new Exception("Town does not have required StringId");

            workshopIndex = town.Workshops.FindIndex(w => w == Object);
        }

        protected override void UnpackInternal()
        {
            Town town = MBObjectManager.Instance.GetObject<Town>(townId);

            Object = town.Workshops[workshopIndex];
        }
    }
}
