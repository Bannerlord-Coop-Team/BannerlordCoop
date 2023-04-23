using Common.Extensions;
using System;
using System.Reflection;
using TaleWorlds.Core;

namespace GameInterface.Serialization.External
{
    /// <summary>
    /// Binary package for HorseComponent
    /// </summary>
    [Serializable]
    public class HorseComponentBinaryPackage : BinaryPackageBase<HorseComponent>
    {
        public HorseComponentBinaryPackage(HorseComponent obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }
    }
}
