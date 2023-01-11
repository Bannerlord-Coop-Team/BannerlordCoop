using System;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class HideoutBinaryPackage : BinaryPackageBase<Hideout>
    {
        private int Index;
        public HideoutBinaryPackage(Hideout obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        protected override void PackInternal()
        {
            Index = Hideout.All.FindIndex(hideout => hideout == Object);
        }

        protected override void UnpackInternal()
        {
            Object = Hideout.All[Index];
        }
    }
}
