using System;
using TaleWorlds.Core;

namespace GameInterface.Serialization.External
{
    /// <summary>
    /// Binary package for HorseComponent
    /// </summary>
    [Serializable]
    public class HorseComponentBinaryPackage : BinaryPackageBase<HorseComponent>
    {
        public string MonsterId;

        public HorseComponentBinaryPackage(HorseComponent obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }
        
        protected override void PackInternal()
        {
            MonsterId = ResolveId(Object.Monster);
            base.PackFields();
        }

        protected override void UnpackInternal()
        {
            base.UnpackFields();

            Object.Monster = ResolveObject<Monster>(MonsterId);
        }
    }
}
