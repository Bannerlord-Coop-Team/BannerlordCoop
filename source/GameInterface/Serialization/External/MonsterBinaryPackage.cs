using System;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.External
{
    /// <summary>
    /// Binary package for Monster
    /// </summary>
    [Serializable]
    public class MonsterBinaryPackage : BinaryPackageBase<Monster>
    {
        public string StringId;

        public MonsterBinaryPackage(Monster obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        protected override void PackInternal()
        {
            StringId = Object.StringId;
        }

        protected override void UnpackInternal()
        {
            MBObjectManager.Instance.GetObject<Monster>(StringId);
        }
    }
}
