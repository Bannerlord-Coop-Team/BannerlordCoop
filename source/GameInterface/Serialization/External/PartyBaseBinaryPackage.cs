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

    public static HashSet<string> Excludes = new HashSet<string>
    {
        "_partyMemberSizeLastCheckVersion",
        "_cachedPartyMemberSizeLimit",
        "_prisonerSizeLastCheckVersion",
        "_cachedPrisonerSizeLimit",
        "_lastNumberOfMenWithHorseVersionNo",
        "_lastNumberOfMenPerTierVersionNo",
        "_customOwner",
        "_mapEventSide",
        "_numberOfHealthyMenPerTier",
        "_ships",
    };

    protected override void PackInternal()
    {
        base.PackFields(Excludes);
    }

    protected override void UnpackInternal()
    {
        base.UnpackFields();

        Object.MobileParty.CreateNewPartyVisual();
    }
}
