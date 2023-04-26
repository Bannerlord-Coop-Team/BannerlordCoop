using Common.Extensions;
using System;
using System.Reflection;
using static TaleWorlds.Core.HorseComponent;

namespace GameInterface.Serialization.External
{
    /// <summary>
    /// Binary package for MaterialProperty
    /// </summary>
    [Serializable]
    public class MaterialPropertyBinaryPackage : BinaryPackageBase<MaterialProperty>
    {
        public MaterialPropertyBinaryPackage(MaterialProperty obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }
    }
}
