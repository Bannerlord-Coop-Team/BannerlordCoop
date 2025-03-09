using System;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class CraftingTemplateBinaryPackage : BinaryPackageBase<CraftingTemplate>
    {
        public string templateId;

        public CraftingTemplateBinaryPackage(CraftingTemplate obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {

        }
        protected override void PackInternal()
        {
            templateId = ResolveId(Object);
        }
        protected override void UnpackInternal()
        {
            Object = ResolveObject<CraftingTemplate>(templateId);
        }
    }
}

