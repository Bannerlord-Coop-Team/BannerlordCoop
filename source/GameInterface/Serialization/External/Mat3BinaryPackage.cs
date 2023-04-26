using Common.Extensions;
using System;
using System.Reflection;
using TaleWorlds.Library;

namespace GameInterface.Serialization.External
{
    /// <summary>
    /// Binary package for Mat3
    /// </summary>
    [Serializable]
    public class Mat3BinaryPackage : BinaryPackageBase<Mat3>
    {
        public Mat3BinaryPackage(Mat3 obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }
    }
}
