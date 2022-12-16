using Common.Extensions;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace GameInterface.Serialization.Impl
{
    /// <summary>
    /// Binary package for PartyBase
    /// </summary>
    [Serializable]
    public class PartyBaseBinaryPackage : BinaryPackageBase<PartyBase>
    {
        public PartyBaseBinaryPackage(PartyBase obj, BinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
        {
        }

        private static HashSet<string> excludes = new HashSet<string>
        {
            "_lastMemberRosterVersionNo",
            "_partyMemberSizeLastCheckVersion",
            "_cachedPartyMemberSizeLimit",
            "_prisonerSizeLastCheckVersion",
            "_cachedPrisonerSizeLimit",
            "_lastNumberOfMenWithHorseVersionNo",
            "_lastNumberOfMenPerTierVersionNo",
            "_cachedTotalStrength",
            "_visual",
        };

        public override void Pack()
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
            
            Campaign.Current?.VisualCreator?.CreatePartyVisual();
        }
    }
}
