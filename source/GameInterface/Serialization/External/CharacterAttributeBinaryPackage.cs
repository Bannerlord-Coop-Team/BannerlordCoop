using System;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.External
{
    /// <summary>
    /// Binary package for CharacterAttribute
    /// </summary>
    [Serializable]
    public class CharacterAttributeBinaryPackage : BinaryPackageBase<CharacterAttribute>
    {
        public string StringId;

        public CharacterAttributeBinaryPackage(CharacterAttribute obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        protected override void PackInternal()
        {
            StringId = Object.StringId;
        }

        protected override void UnpackInternal()
        {
            Object = MBObjectManager.Instance.GetObject<CharacterAttribute>(StringId);
        }
    }
}
