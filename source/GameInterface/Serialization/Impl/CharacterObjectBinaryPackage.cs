using Common.Extensions;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Serialization.Impl
{
    /// <summary>
    /// Binary package for CharacterObject
    /// </summary>
    [Serializable]
    public class CharacterObjectBinaryPackage : BinaryPackageBase<CharacterObject>
    {
        string stringId;
        public CharacterObjectBinaryPackage(CharacterObject obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        public override void Pack()
        {
            stringId = Object.StringId;
            // TODO implement
        }

        protected override void UnpackInternal()
        {
            Object = MBObjectManager.Instance.GetObject<CharacterObject>(stringId);
            // TODO implement
        }
    }
}
