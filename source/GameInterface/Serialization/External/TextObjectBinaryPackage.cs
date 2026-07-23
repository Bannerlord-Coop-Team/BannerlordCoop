using System;
using System.Collections.Generic;
using TaleWorlds.Localization;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class TextObjectBinaryPackage : BinaryPackageBase<TextObject>
    {
        public TextObjectBinaryPackage(TextObject obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        static readonly HashSet<string> excludes = new HashSet<string>
        {
            "cachedTokens",
            "cachedTextLanguageId"
        };

        protected override void PackInternal()
        {
            base.PackFields(excludes);
        }

        protected override void UnpackInternal()
        {
            base.UnpackFields();
        }
    }
}
