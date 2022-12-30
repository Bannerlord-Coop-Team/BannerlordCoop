using System;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.Impl
{
    [Serializable]
    public class CraftingTemplateBinaryPackage : BinaryPackageBase<CraftingTemplate>
    {
        public string templateId;

        public CraftingTemplateBinaryPackage(CraftingTemplate obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {

        }
        protected override void PackInternal()
        {
            templateId = Object.StringId;
        }
        protected override void UnpackInternal()
        {
            Object = MBObjectManager.Instance.GetObject<CraftingTemplate>(templateId);
        }
    }
}

