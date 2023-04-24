using Common.Extensions;
using System;
using System.Reflection;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class AttackCollisionDataBinaryPackage : BinaryPackageBase<AttackCollisionData>
    {
        public AttackCollisionDataBinaryPackage(AttackCollisionData obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }
    }
}
