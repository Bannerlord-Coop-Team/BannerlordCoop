using System;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace GameInterface.Serialization.Impl
{
    [Serializable]
    public class HideoutBinaryPackage : BinaryPackageBase<Hideout>
    {
        private int Index;
        public HideoutBinaryPackage(Hideout obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        public override void Pack()
        {
            Index = Hideout.All.FindIndex(hideout => hideout == Object);
        }

        protected override void UnpackInternal()
        {
            Object = Hideout.All[Index];
        }
    }
}
