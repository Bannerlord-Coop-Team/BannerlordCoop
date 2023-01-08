using Common.Extensions;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Serialization.External
{
    /// <summary>
    /// Binary package for TroopRosterElement
    /// </summary>
    [Serializable]
    public class TroopRosterElementBinaryPackage : BinaryPackageBase<TroopRosterElement>
    {
        public TroopRosterElementBinaryPackage(TroopRosterElement obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        protected override void PackInternal()
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
