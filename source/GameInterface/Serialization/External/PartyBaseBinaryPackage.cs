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
        public PartyBaseBinaryPackage(PartyBase obj, BinaryPackageFactory binaryPackageFactory) : base(obj,
            binaryPackageFactory)
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

        private static readonly FieldInfo PartyBase_Visual = typeof(PartyBase).GetField("_visual", BindingFlags.NonPublic | BindingFlags.Instance);
        
        protected override void PackInternal()
        {
            base.PackInternal(excludes);
        }

        protected override void UnpackInternal()
        {
            base.UnpackInternal();
            
            IPartyVisual partyVisual = Campaign.Current?.VisualCreator?.CreatePartyVisual();
            PartyBase_Visual.SetValue(Object, partyVisual);
        }
    }
}
