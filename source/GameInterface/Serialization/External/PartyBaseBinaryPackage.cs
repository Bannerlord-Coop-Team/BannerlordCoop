using Common.Extensions;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Serialization.External
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

        protected override void PackInternal()
        {
            foreach (FieldInfo field in ObjectType.GetAllInstanceFields(excludes))
            {
                object obj = field.GetValue(Object);
                StoredFields.Add(field, BinaryPackageFactory.GetBinaryPackage(obj));
            }
        }

        private static readonly FieldInfo PartyBase_Visual = typeof(PartyBase).GetField("_visual", BindingFlags.NonPublic | BindingFlags.Instance);
        protected override void UnpackInternal()
        {
            TypedReference reference = __makeref(Object);
            foreach (FieldInfo field in StoredFields.Keys)
            {
                field.SetValueDirect(reference, StoredFields[field].Unpack());
            }

            IPartyVisual partyVisual = Campaign.Current?.VisualCreator?.CreatePartyVisual();
            PartyBase_Visual.SetValue(Object, partyVisual);
        }
    }
}
