using Common.Extensions;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Localization;

namespace GameInterface.Serialization.External
{
    [Serializable]
    public class TextObjectBinaryPackage : BinaryPackageBase<TextObject>
    {
        public TextObjectBinaryPackage(TextObject obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        static readonly HashSet<string> excludes = new HashSet<string>
        {
            "cachedTokens",
            "cachedTextLanguageId"
        };

        protected override void PackInternal()
        {
            foreach (FieldInfo field in ObjectType.GetAllInstanceFields(excludes))
            {
                object obj = field.GetValue(Object);
                StoredFields.Add(field, BinaryPackageFactory.GetBinaryPackage(obj));
            }
        }

        protected override void UnpackInternal()
        {
            TypedReference reference = __makeref(Object);
            foreach (FieldInfo field in StoredFields.Keys)
            {
                field.SetValueDirect(reference, StoredFields[field].Unpack());
            }
        }
    }
}
