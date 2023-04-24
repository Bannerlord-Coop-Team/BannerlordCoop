using Common.Extensions;
using System;
using System.Reflection;
using TaleWorlds.Core;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class TradeItemComponentBinaryPackage : BinaryPackageBase<TradeItemComponent>
    {
        public TradeItemComponentBinaryPackage(TradeItemComponent obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }
    }
}