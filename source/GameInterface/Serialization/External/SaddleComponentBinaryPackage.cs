using Common.Extensions;
using System;
using System.Reflection;
using TaleWorlds.Core;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class SaddleComponentBinaryPackage : BinaryPackageBase<SaddleComponent>
    {

        public SaddleComponentBinaryPackage(SaddleComponent obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }
    }
}
