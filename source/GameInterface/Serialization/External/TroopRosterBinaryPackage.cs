using Common.Extensions;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Serialization.External
{
    /// <summary>
    /// Binary package for TroopRoster
    /// </summary>
    [Serializable]
    public class TroopRosterBinaryPackage : BinaryPackageBase<TroopRoster>
    {
        public TroopRosterBinaryPackage(TroopRoster obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        private static HashSet<string> excludes = new HashSet<string>
        {
            "_troopRosterElements",
            "_troopRosterElementsVersion",
            "<NumberChangedCallback>k__BackingField",
            "<VersionNo>k__BackingField",
        };

        protected override void PackInternal()
        {
            foreach (FieldInfo field in ObjectType.GetAllInstanceFields(excludes))
            {
                object obj = field.GetValue(Object);
                StoredFields.Add(field, BinaryPackageFactory.GetBinaryPackage(obj));
            }
        }

        private static PropertyInfo OwnerParty = typeof(TroopRoster).GetProperty("OwnerParty", BindingFlags.NonPublic | BindingFlags.Instance);
        private static PropertyInfo NumberChangedCallback = typeof(TroopRoster).GetProperty("NumberChangedCallback", BindingFlags.NonPublic | BindingFlags.Instance);

        private static MethodInfo MemberRosterNumberChanged = typeof(PartyBase).GetMethod("MemberRosterNumberChanged", BindingFlags.NonPublic | BindingFlags.Instance);
        protected override void UnpackInternal()
        {
            TypedReference reference = __makeref(Object);
            foreach (FieldInfo field in StoredFields.Keys)
            {
                field.SetValueDirect(reference, StoredFields[field].Unpack());
            }

            PartyBase ownerParty = (PartyBase)OwnerParty.GetValue(Object);
            Type delegateType = NumberChangedCallback.PropertyType;

            if(delegateType.TryCreateDelegate(ownerParty, MemberRosterNumberChanged, out Delegate @delegate))
            {
                NumberChangedCallback.SetValue(Object, @delegate);
            }
        }
    }
}
