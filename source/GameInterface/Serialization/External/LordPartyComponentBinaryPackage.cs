using Common.Extensions;
using GameInterface.Extentions;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Serialization.External
{
    /// <summary>
    /// Binary package for LordPartyComponent
    /// </summary>
    [Serializable]
    public class LordPartyComponentBinaryPackage : BinaryPackageBase<LordPartyComponent>
    {
        public LordPartyComponentBinaryPackage(LordPartyComponent obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        private static readonly HashSet<string> excludes = new HashSet<string>
        {
            "_cachedName",
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

            MethodInfo Initialize = typeof(PartyComponent).GetMethod("Initialize", BindingFlags.NonPublic | BindingFlags.Instance);
            Initialize.Invoke(Object, new object[] { Object.MobileParty });
        }
    }
}
