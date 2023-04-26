using Common.Extensions;
using System;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class ItemObjectBinaryPackage : BinaryPackageBase<ItemObject>
    {

        public string stringId;

        public ItemObjectBinaryPackage(ItemObject obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        protected override void PackInternal()
        {
            stringId = Object.StringId;
            
            base.PackInternal();
        }

        protected override void UnpackInternal()
        {
            if(stringId != null)
            {
                var newObject = ResolveId<ItemObject>(stringId);
                if(newObject != null)
                {
                    Object = newObject;
                    return;
                }
            }

            base.UnpackInternal();
        }
    }
}
