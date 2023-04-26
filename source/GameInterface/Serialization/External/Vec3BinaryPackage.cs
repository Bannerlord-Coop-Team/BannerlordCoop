using Common.Extensions;
using System;
using System.Reflection;
using TaleWorlds.Library;

namespace GameInterface.Serialization.External
{
    /// <summary>
    /// Binary package for Vec3
    /// </summary>
    [Serializable]
    public class Vec3BinaryPackage : BinaryPackageBase<Vec3>
    {
        public Vec3BinaryPackage(Vec3 obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }
    }
}
