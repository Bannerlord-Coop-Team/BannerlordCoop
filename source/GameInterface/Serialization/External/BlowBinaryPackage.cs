using Common.Extensions;
using System;
using System.Reflection;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class BlowBinaryPackage : BinaryPackageBase<Blow>
    {
        public BlowBinaryPackage(Blow obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }
    }
}
