using Common.Extensions;
using System;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Serialization.Impl
{
    /// <summary>
    /// Binary package for PathFaceRecord
    /// </summary>
    [Serializable]
    public class PathFaceRecordBinaryPackage : BinaryPackageBase<PathFaceRecord>
    {
        public PathFaceRecordBinaryPackage(PathFaceRecord obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        public override void Pack()
        {
            foreach (FieldInfo field in ObjectType.GetAllInstanceFields())
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
