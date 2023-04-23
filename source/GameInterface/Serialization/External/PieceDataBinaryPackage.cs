using Common.Extensions;
using System;
using System.Reflection;
using TaleWorlds.Core;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class PieceDataBinaryPackage : BinaryPackageBase<PieceData>
    {
        public PieceDataBinaryPackage(PieceData obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }
    }
    
}