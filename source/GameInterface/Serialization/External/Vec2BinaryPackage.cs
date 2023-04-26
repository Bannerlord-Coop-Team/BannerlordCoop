using Common.Extensions;
using System;
using System.Reflection;
using TaleWorlds.Library;

namespace GameInterface.Serialization.External
{
    /// <summary>
    /// Binary package for Vec2
    /// </summary>
    [Serializable]
    public class Vec2BinaryPackage : BinaryPackageBase<Vec2>
    {
        public Vec2BinaryPackage(Vec2 obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }
    }
}
