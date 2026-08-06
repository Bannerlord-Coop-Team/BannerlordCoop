using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace GameInterface.Services.Issues.Patches;

internal sealed class VillageNeedsToolsIssueOwnershipSaveData
{
    [SaveableField(1)]
    internal Hero IssueGiverHero;

    [SaveableField(2)]
    internal string OwnerControllerId;

    private VillageNeedsToolsIssueOwnershipSaveData()
    {
    }

    internal VillageNeedsToolsIssueOwnershipSaveData(Hero issueGiverHero, string ownerControllerId)
    {
        IssueGiverHero = issueGiverHero;
        OwnerControllerId = ownerControllerId;
    }
}

public sealed class VillageNeedsToolsIssueOwnershipSaveableTypeDefiner : SaveableTypeDefiner
{
    // Must not collide with other SaveableTypeDefiner base ids (SiegeAftermath=44_177_000,
    // PlayerCaptivityAttackProtection=44_182_000).
    private const int SaveBaseId = 44_183_000;

    public VillageNeedsToolsIssueOwnershipSaveableTypeDefiner() : base(SaveBaseId)
    {
    }

    public override void DefineClassTypes()
    {
        AddClassDefinition(typeof(VillageNeedsToolsIssueOwnershipSaveData), 1);
    }

    public override void DefineContainerDefinitions()
    {
        ConstructContainerDefinition(typeof(List<VillageNeedsToolsIssueOwnershipSaveData>));
    }
}
