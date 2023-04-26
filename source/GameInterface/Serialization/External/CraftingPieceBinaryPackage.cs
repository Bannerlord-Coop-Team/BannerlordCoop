using Common.Extensions;
using System;
using System.Reflection;
using TaleWorlds.Core;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class CraftingPieceBinaryPackage : BinaryPackageBase<CraftingPiece>
    {
        public CraftingPieceBinaryPackage(CraftingPiece obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }
    }
}