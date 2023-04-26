using Common.Extensions;
using System;
using System.Reflection;
using TaleWorlds.Library;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class MatrixFrameBinaryPackage : BinaryPackageBase<MatrixFrame>
    {
        public MatrixFrameBinaryPackage(MatrixFrame obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }
    }
}