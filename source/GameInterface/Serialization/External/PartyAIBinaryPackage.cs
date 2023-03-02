using Common.Extensions;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Serialization.External
{
    /// <summary>
    /// Binary package for PartyAI
    /// </summary>
    [Serializable]
    public class PartyAIBinaryPackage : BinaryPackageBase<MobilePartyAi>
    {
        public PartyAIBinaryPackage(MobilePartyAi obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        private static readonly HashSet<string> excludes = new HashSet<string>
        {
            "<DefaultBehaviorTarget>k__BackingField",
            "<AiStateObject>k__BackingField",
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
