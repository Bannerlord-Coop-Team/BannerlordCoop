using GameInterface.Services.Smithing;
using System;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class CraftingPlayerDataBinaryPackage : BinaryPackageBase<CraftingPlayerData>
    {
        public CraftingPlayerDataBinaryPackage(CraftingPlayerData obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        protected override void PackInternal()
        {
            base.PackFields();
        }

        protected override void UnpackInternal()
        {
            base.UnpackFields();
        }
    }
}
