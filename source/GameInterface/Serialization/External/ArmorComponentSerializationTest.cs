using Common.Extensions;
using System;
using System.Reflection;
using TaleWorlds.Core;

namespace GameInterface.Serialization.External
{
    /// <summary>
    /// Binary package for ArmorComponent
    /// </summary>
    [Serializable]
    public class ArmorComponentBinaryPackage : BinaryPackageBase<ArmorComponent>
    {
        public ArmorComponentBinaryPackage(ArmorComponent obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }
    }
}
