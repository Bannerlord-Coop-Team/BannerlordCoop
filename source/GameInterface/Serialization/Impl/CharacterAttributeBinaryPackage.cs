using Common.Extensions;
using System;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.Impl
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
