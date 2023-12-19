using GameInterface.Services.PartyVisuals.Extensions;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Serialization.External;

/// <summary>
/// Binary package for PartyBase
/// </summary>
[Serializable]
public class PartyBaseBinaryPackage : BinaryPackageBase<PartyBase>
{
    public PartyBaseBinaryPackage(PartyBase obj, IBinaryPackageFactory binaryPackageFactory) : base(obj, binaryPackageFactory)
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
    };

    protected override void PackInternal()
    {
        base.PackFields(excludes);
    }

    protected override void UnpackInternal()
    {
        base.UnpackFields();

        Object.CreateNewPartyVisual();
    }
}
