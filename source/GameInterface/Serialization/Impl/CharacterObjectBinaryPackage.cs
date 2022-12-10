using Common.Extensions;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Serialization.Impl
{
    /// <summary>
    /// Binary package for CharacterObject
    /// </summary>
    [Serializable]
    public class CharacterObjectBinaryPackage : BinaryPackageBase<CharacterObject>
    {
        public CharacterObjectBinaryPackage(CharacterObject obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        public override void Pack()
        {
            // TODO implement
        }

        protected override void UnpackInternal()
        {
            // TODO implement
        }
    }
}
